using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Babel.Player.Models;
using Babel.Player.Services.Credentials;
using Babel.Player.Services.Registries;
using Babel.Player.Services.Settings;

namespace Babel.Player.Services;

/// <summary>
/// Diarization provider backed by the managed CPU runtime and the upstream WeSpeaker Python package.
/// </summary>
public sealed class WeSpeakerCpuDiarizationProvider : PythonSubprocessServiceBase, IDiarizationProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ManagedCpuRuntimeManager _cpuRuntimeManager;

    public WeSpeakerCpuDiarizationProvider(AppLog log)
        : this(log, new ManagedCpuRuntimeManager(log))
    {
    }

    internal WeSpeakerCpuDiarizationProvider(AppLog log, ManagedCpuRuntimeManager cpuRuntimeManager)
        : base(log, cpuRuntimeManager)
    {
        _cpuRuntimeManager = cpuRuntimeManager;
    }

    public ProviderReadiness CheckReadiness(AppSettings settings, ApiKeyStore? keyStore)
    {
        if (_cpuRuntimeManager.State == ManagedCpuState.Failed)
        {
            return new ProviderReadiness(
                false,
                $"Managed CPU runtime is not ready for WeSpeaker: {_cpuRuntimeManager.FailureReason ?? "bootstrap failed"}");
        }

        if (_cpuRuntimeManager.NeedsBootstrap)
        {
            return new ProviderReadiness(
                false,
                "Managed CPU runtime is not bootstrapped for WeSpeaker yet.");
        }

        return ProviderReadiness.Ready;
    }

    public async Task<bool> EnsureReadyAsync(
        AppSettings settings,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            await _cpuRuntimeManager.EnsureInstalledAsync(cancellationToken: ct).ConfigureAwait(false);
            progress?.Report(1.0);
            return _cpuRuntimeManager.State == ManagedCpuState.Ready;
        }
        catch (Exception ex)
        {
            Log.Error($"Managed CPU runtime bootstrap failed for WeSpeaker: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<DiarizationResult> DiarizeAsync(
        DiarizationRequest request,
        CancellationToken ct = default)
    {
        if (!File.Exists(request.SourceAudioPath))
            throw new FileNotFoundException($"Audio file not found: {request.SourceAudioPath}");

        Log.Info($"[WeSpeakerCpuDiarization] Diarizing: {request.SourceAudioPath}");

        try
        {
            var result = await RunPythonScriptAsync(
                DiarizeScript,
                [request.SourceAudioPath],
                "wespeaker_diarize",
                cancellationToken: ct).ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                Log.Error($"WeSpeaker CPU diarization failed (exit {result.ExitCode})", new Exception(result.Stderr));
                return new DiarizationResult(false, [], 0, string.IsNullOrWhiteSpace(result.Stderr) ? result.Stdout : result.Stderr);
            }

            var payload = JsonSerializer.Deserialize<WeSpeakerDiarizationPayloadDto>(result.Stdout, JsonOptions)
                ?? throw new InvalidOperationException("WeSpeaker diarization returned no JSON payload.");

            var normalizedSegments = NormalizeSegments(payload.Segments ?? []);
            var speakerCount = normalizedSegments.Count == 0
                ? 0
                : normalizedSegments.Select(segment => segment.SpeakerId).Distinct(StringComparer.Ordinal).Count();

            return new DiarizationResult(true, normalizedSegments, speakerCount, null);
        }
        catch (Exception ex)
        {
            Log.Error($"WeSpeaker CPU diarization failed: {ex.Message}", ex);
            return new DiarizationResult(false, [], 0, ex.Message);
        }
    }

    private static IReadOnlyList<DiarizedSegment> NormalizeSegments(IReadOnlyList<WeSpeakerRawSegmentDto> segments)
    {
        var normalized = new List<DiarizedSegment>(segments.Count);
        var assignedLabels = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var segment in segments)
        {
            normalized.Add(new DiarizedSegment(
                segment.Start,
                segment.End,
                NormalizeSpeakerId(segment.SpeakerId, assignedLabels)));
        }

        return normalized;
    }

    private static string NormalizeSpeakerId(string? rawSpeakerId, IDictionary<string, string> assignedLabels)
    {
        var key = string.IsNullOrWhiteSpace(rawSpeakerId)
            ? $"speaker_{assignedLabels.Count}"
            : rawSpeakerId.Trim();

        if (assignedLabels.TryGetValue(key, out var existing))
            return existing;

        var normalized = $"spk_{assignedLabels.Count:00}";
        assignedLabels[key] = normalized;
        return normalized;
    }

    private const string DiarizeScript = """
import json
import sys
import traceback

import wespeaker

def _coerce_float(value):
    try:
        return float(value)
    except Exception:
        return 0.0

def _coerce_string(value):
    return "" if value is None else str(value)

def _extract_items(raw_result):
    if raw_result is None:
        return []
    if isinstance(raw_result, dict):
        for key in ("segments", "result", "data", "diarization"):
            value = raw_result.get(key)
            if value is not None:
                return value
        return []
    return raw_result

def _parse_item(item):
    if isinstance(item, dict):
        return {
            "utt_id": _coerce_string(item.get("utt_id") or item.get("id")),
            "start": _coerce_float(item.get("start") or item.get("start_time") or item.get("begin")),
            "end": _coerce_float(item.get("end") or item.get("end_time") or item.get("stop")),
            "speaker_id": _coerce_string(
                item.get("speaker_id")
                or item.get("speaker")
                or item.get("speaker_label")
                or item.get("label")
            ),
        }

    if isinstance(item, (list, tuple)):
        if len(item) >= 4:
            return {
                "utt_id": _coerce_string(item[0]),
                "start": _coerce_float(item[1]),
                "end": _coerce_float(item[2]),
                "speaker_id": _coerce_string(item[3]),
            }
        if len(item) == 3:
            return {
                "utt_id": "",
                "start": _coerce_float(item[0]),
                "end": _coerce_float(item[1]),
                "speaker_id": _coerce_string(item[2]),
            }

    return None

def main():
    audio_path = sys.argv[1]
    model = wespeaker.load_model("english")
    model.set_device("cpu")
    raw_result = model.diarize(audio_path)

    segments = []
    for item in _extract_items(raw_result):
        parsed = _parse_item(item)
        if parsed is not None:
            segments.append(parsed)

    print(json.dumps({"segments": segments}, ensure_ascii=False))

if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"WeSpeaker diarization failed: {exc}", file=sys.stderr)
        traceback.print_exc()
        sys.exit(1)
""";

    private sealed class WeSpeakerDiarizationPayloadDto
    {
        [JsonPropertyName("segments")]
        public List<WeSpeakerRawSegmentDto>? Segments { get; set; }
    }

    private sealed class WeSpeakerRawSegmentDto
    {
        [JsonPropertyName("utt_id")]
        public string? UtteranceId { get; set; }

        [JsonPropertyName("start")]
        public double Start { get; set; }

        [JsonPropertyName("end")]
        public double End { get; set; }

        [JsonPropertyName("speaker_id")]
        public string? SpeakerId { get; set; }
    }
}
