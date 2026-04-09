using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Babel.Player.Models;
using Babel.Player.Services.Credentials;
using Babel.Player.Services.Registries;
using Babel.Player.Services.Settings;

namespace Babel.Player.Services;

/// <summary>
/// Diarization provider backed by the containerized WeSpeaker CPU fallback endpoint.
/// </summary>
public sealed class WeSpeakerContainerizedDiarizationProvider : IDiarizationProvider
{
    private readonly ContainerizedInferenceClient _client;
    private readonly AppLog _log;
    private readonly ContainerizedServiceProbe? _probe;

    public WeSpeakerContainerizedDiarizationProvider(
        ContainerizedInferenceClient client,
        AppLog log,
        ContainerizedServiceProbe? probe = null)
    {
        _client = client;
        _log = log;
        _probe = probe;
    }

    public ProviderReadiness CheckReadiness(AppSettings settings, ApiKeyStore? keyStore) =>
        ContainerizedProviderReadiness.CheckDiarization(settings, ProviderNames.WeSpeakerLocal, _probe, keyStore);

    public async Task<bool> EnsureReadyAsync(
        AppSettings settings,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report(1.0);
        if (_probe is null)
            return CheckReadiness(settings, null).IsReady;

        var readiness = await ContainerizedProviderReadiness.CheckDiarizationForExecutionAsync(
            settings,
            ProviderNames.WeSpeakerLocal,
            _probe,
            ct);
        return readiness.IsReady;
    }

    public async Task<DiarizationResult> DiarizeAsync(
        DiarizationRequest request,
        CancellationToken ct = default)
    {
        if (!File.Exists(request.SourceAudioPath))
            throw new FileNotFoundException($"Audio file not found: {request.SourceAudioPath}");

        _log.Info($"[WeSpeakerContainerizedDiarization] Diarizing: {request.SourceAudioPath}");
        return await _client.DiarizeAsync(request.SourceAudioPath, "wespeaker", request.MinSpeakers, request.MaxSpeakers, ct);
    }
}
