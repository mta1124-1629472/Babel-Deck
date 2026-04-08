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
public sealed class ElevenLabsTtsProvider : ITtsProvider
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
    /// Initializes a new <see cref="ElevenLabsTtsProvider"/> and prepares a lazily constructed ElevenLabs API client used for text-to-speech requests.
    /// </summary>
    /// <param name="log">Application logger used for informational and diagnostic messages.</param>
    /// <param name="apiKey">API key for the ElevenLabs service.</param>
    /// <param name="clientFactory">Optional factory to create an <see cref="ElevenLabsApiClient"/>; if null, a client will be created using the provided <paramref name="apiKey"/>.</param>
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
    /// Checks whether the provider has the ElevenLabs API key configured.
    /// </summary>
    /// <param name="settings">Application settings (not used by this provider).</param>
    /// <param name="keyStore">Optional API key store (not used by this provider).</param>
    /// <returns>A ProviderReadiness indicating readiness. If the configured ElevenLabs API key is missing or whitespace, the readiness is not ready and contains an explanatory message.</returns>
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
    /// Generates a single audio file by combining all translated segments from a translation artifact.
    /// </summary>
    /// <param name="request">Request containing the path to the translation JSON (TranslationJsonPath), the desired output audio path (OutputAudioPath), and the VoiceName used to select the ElevenLabs model.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A TtsResult with success=true, the output audio path, the voice name, the generated audio byte length, and null error on success.</returns>
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
    /// Generates TTS audio for a single translated segment and writes the resulting audio file to disk.
    /// </summary>
    /// <param name="request">Single segment TTS request containing the text to synthesize, the target output file path, and the desired voice name.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A TtsResult containing success state, the output audio path, the voice name used, and the number of bytes written.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="request"/>.Text is null, empty, or consists only of whitespace.</exception>
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
}
