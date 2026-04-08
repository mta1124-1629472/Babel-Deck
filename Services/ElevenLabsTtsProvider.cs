using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Babel.Player.Models;
using Babel.Player.Services.Credentials;
using Babel.Player.Services.Registries;
using Babel.Player.Services.Settings;

namespace Babel.Player.Services;

/// <summary>
/// TTS provider backed by the ElevenLabs REST API.
///
/// Voice selection in Babel Player maps the "voice" field (AppSettings.TtsVoice)
/// to the ElevenLabs <em>model</em> ID (quality tier, e.g.
/// <c>eleven_multilingual_v2</c>). The character voice used for synthesis is
/// <see cref="DefaultVoiceId"/> (Rachel) — a pre-made ElevenLabs voice available
/// on all subscription tiers. Future work can expose per-user voice ID selection.
/// </summary>
public sealed class ElevenLabsTtsProvider : ITtsProvider, IDisposable
{
    /// <summary>
    /// ElevenLabs pre-made "Rachel" voice — available on all subscription tiers.
    /// Used as the default character voice for all synthesis requests.
    /// </summary>
    public const string DefaultVoiceId = "21m00Tcm4TlvDq8ikWAM";

    private readonly AppLog _log;
    private readonly string _apiKey;
    private readonly Lazy<ElevenLabsApiClient> _clientLazy;

    /// <summary>
    /// Creates a new ElevenLabsTtsProvider and configures a lazily initialized ElevenLabsApiClient.
    /// </summary>
    /// <param name="log">Application logger used for provider diagnostics.</param>
    /// <param name="apiKey">ElevenLabs API key used to authenticate requests.</param>
    /// <param name="clientFactory">Optional factory to create an ElevenLabsApiClient; if null a default factory that uses <paramref name="apiKey"/> will be used and the client instance will be created on first use.</param>
    public ElevenLabsTtsProvider(
        AppLog log,
        string apiKey,
        Func<ElevenLabsApiClient>? clientFactory = null)
    {
        _log = log;
        _apiKey = apiKey;
        _clientLazy = new Lazy<ElevenLabsApiClient>(clientFactory ?? (() => new ElevenLabsApiClient(_apiKey)), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Determines whether the ElevenLabs TTS provider is configured and ready to use.
    /// </summary>
    /// <returns>
    /// `ProviderReadiness.Ready` if an ElevenLabs API key is configured; otherwise a `ProviderReadiness` with `Success = false` and an explanatory message.
    /// </returns>
    public ProviderReadiness CheckReadiness(AppSettings settings, ApiKeyStore? keyStore = null)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return new ProviderReadiness(false, "ElevenLabs API key is not set.");

        return ProviderReadiness.Ready;
    }

    /// <summary>
    /// Generates speech for all translated segments combined into one audio file.
    /// <paramref name="request.VoiceName"/> maps to the ElevenLabs model ID
    /// (quality tier); <see cref="DefaultVoiceId"/> is used for character voice.
    /// <summary>
    /// Generate a single combined audio file by synthesizing all non-empty translated segments from the translation artifact.
    /// </summary>
    /// <param name="request">Request containing the path to the translation JSON (TranslationJsonPath), desired output audio path (OutputAudioPath), and VoiceName to select the ElevenLabs model.</param>
    /// <returns>A TtsResult describing the output path, selected voice name, byte length, and success state.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the translation JSON file at <c>request.TranslationJsonPath</c> does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the translation artifact contains no non-empty translated text segments.</exception>
    public async Task<TtsResult> GenerateTtsAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.TranslationJsonPath))
            throw new FileNotFoundException($"Translation file not found: {request.TranslationJsonPath}");

        var translationArtifact = await ArtifactJson.LoadTranslationAsync(request.TranslationJsonPath, cancellationToken);
        var texts = (translationArtifact.Segments ?? [])
            .Select(s => s.TranslatedText ?? string.Empty)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        if (texts.Count == 0)
            throw new InvalidOperationException("No translated text found in translation artifact.");

        var combinedText = string.Join(" ", texts);
        var modelId = NormalizeModelId(request.VoiceName);

        _log.Info($"[ElevenLabsTTS] Generating combined audio: {texts.Count} segments, model={modelId}");

        var client = _clientLazy.Value;
        var audioBytes = await client.TextToSpeechAsync(combinedText, DefaultVoiceId, modelId, cancellationToken);

        var outputDir = Path.GetDirectoryName(request.OutputAudioPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        await File.WriteAllBytesAsync(request.OutputAudioPath, audioBytes, cancellationToken);

        _log.Info($"[ElevenLabsTTS] Combined audio written: {request.OutputAudioPath} ({audioBytes.Length} bytes)");

        return new TtsResult(true, request.OutputAudioPath, request.VoiceName, audioBytes.Length, null);
    }

    /// <summary>
    /// Generates speech for a single translated text segment.
    /// <paramref name="request.VoiceName"/> maps to the ElevenLabs model ID;
    /// <see cref="DefaultVoiceId"/> is used for character voice.
    /// <summary>
    /// Generates speech audio for a single translated segment and writes the resulting audio file to the request's output path.
    /// </summary>
    /// <param name="request">Single-segment TTS request. `request.Text` must be non-empty; `request.OutputAudioPath` is the file path to write; `request.VoiceName` selects the synthesis model.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A TtsResult describing the operation: success flag, output path, voice name, byte length of the written audio, and any error (null on success).</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="request"/> has an empty or whitespace <c>Text</c> value.</exception>
    public async Task<TtsResult> GenerateSegmentTtsAsync(
        SingleSegmentTtsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new ArgumentException("Segment text cannot be empty.", nameof(request));

        var modelId = NormalizeModelId(request.VoiceName);

        _log.Info($"[ElevenLabsTTS] Generating segment audio: {request.Text[..Math.Min(30, request.Text.Length)]}... model={modelId}");

        var client = _clientLazy.Value;
        var audioBytes = await client.TextToSpeechAsync(request.Text, DefaultVoiceId, modelId, cancellationToken);

        var outputDir = Path.GetDirectoryName(request.OutputAudioPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        await File.WriteAllBytesAsync(request.OutputAudioPath, audioBytes, cancellationToken);

        _log.Info($"[ElevenLabsTTS] Segment audio written: {request.OutputAudioPath} ({audioBytes.Length} bytes)");

        return new TtsResult(true, request.OutputAudioPath, request.VoiceName, audioBytes.Length, null);
    }

    // Map the VoiceName field (which holds the selected model/quality tier in Babel Player's
    // ElevenLabs configuration) to a valid ElevenLabs model ID. Falls back to the
    // highest-quality multilingual model if the value is unrecognised or empty.
    private static string NormalizeModelId(string voiceName) =>
        voiceName switch
        {
            "eleven_multilingual_v2"  => "eleven_multilingual_v2",
            "eleven_turbo_v2_5"       => "eleven_turbo_v2_5",
            "eleven_flash_v2_5"       => "eleven_flash_v2_5",
            _                         => "eleven_multilingual_v2",
        };

    public void Dispose()
    {
        if (_clientLazy.IsValueCreated)
            _clientLazy.Value.Dispose();
    }
}
