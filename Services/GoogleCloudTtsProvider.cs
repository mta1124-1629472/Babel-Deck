using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Babel.Player.Models;
using Babel.Player.Services.Credentials;
using Babel.Player.Services.Registries;
using Babel.Player.Services.Settings;

namespace Babel.Player.Services;

/// <summary>
/// Google Cloud TTS provider wired for cloud runtime only.
///
/// The UI model selection (standard/wavenet/neural2) maps to a default voice name.
/// This keeps parity with existing provider-model UX where <see cref="TtsRequest.VoiceName"/>
/// carries the selected model option.
/// </summary>
public sealed class GoogleCloudTtsProvider : ITtsProvider
{
    private readonly AppLog _log;
    private readonly string _apiKey;
    private readonly Lazy<GoogleApiClient> _clientLazy;

    /// <summary>
    /// Initializes a GoogleCloudTtsProvider with the specified logger, API key, and an optional Google API client factory.
    /// </summary>
    /// <param name="log">Application logger used for informational messages.</param>
    /// <param name="apiKey">Google Cloud Text-to-Speech API key used for authenticating requests.</param>
    /// <param name="clientFactory">Optional factory to create a <see cref="GoogleApiClient"/>; when null a default client will be created and instantiated lazily.</param>
    public GoogleCloudTtsProvider(
        AppLog log,
        string apiKey,
        Func<GoogleApiClient>? clientFactory = null)
    {
        _log = log;
        _apiKey = apiKey;
        _clientLazy = new Lazy<GoogleApiClient>(clientFactory ?? (() => new GoogleApiClient(_apiKey)), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Checks whether the provider is ready by verifying the configured Google Cloud API key.
    /// </summary>
    /// <param name="settings">Application settings (not used by this provider).</param>
    /// <param name="keyStore">Optional API key store (not used by this provider).</param>
    /// <returns>A <see cref="ProviderReadiness"/> indicating readiness; returns not ready when the API key is missing, ready otherwise.</returns>
    public ProviderReadiness CheckReadiness(AppSettings settings, ApiKeyStore? keyStore = null)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return new ProviderReadiness(false, "API key missing for provider 'Google Cloud TTS'.");

        return ProviderReadiness.Ready;
    }

    /// <summary>
    /// Synthesizes speech from the translated segments in a translation artifact and writes the combined audio to the requested output path.
    /// </summary>
    /// <param name="request">Request containing the translation JSON path, desired output audio path, and optional voice name.</param>
    /// <returns>A <see cref="TtsResult"/> representing the successful generation: output path, voice name, and produced byte length.</returns>
    /// <exception cref="FileNotFoundException">If the translation JSON file specified by <paramref name="request"/> does not exist.</exception>
    /// <exception cref="InvalidOperationException">If the translation artifact contains no translated text to synthesize.</exception>
    public async Task<TtsResult> GenerateTtsAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.TranslationJsonPath))
            throw new FileNotFoundException($"Translation file not found: {request.TranslationJsonPath}");

        var translation = await ArtifactJson.LoadTranslationAsync(request.TranslationJsonPath, cancellationToken);
        var combinedText = string.Join(" ",
            (translation.Segments ?? [])
                .Select(s => s.TranslatedText)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!.Trim()));

        if (string.IsNullOrWhiteSpace(combinedText))
            throw new InvalidOperationException("No translated text found in translation artifact.");

        var voiceName = ResolveVoiceName(request.VoiceName);
        var client = _clientLazy.Value;
        var audioBytes = await client.SynthesizeSpeechAsync(combinedText, voiceName, cancellationToken);

        var outputDir = Path.GetDirectoryName(request.OutputAudioPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        await File.WriteAllBytesAsync(request.OutputAudioPath, audioBytes, cancellationToken);

        _log.Info($"[GoogleCloudTTS] Generated combined audio: {request.OutputAudioPath} ({audioBytes.Length} bytes)");

        return new TtsResult(true, request.OutputAudioPath, request.VoiceName, audioBytes.Length, null);
    }

    /// <summary>
    /// Synthesizes speech for a single text segment and writes the resulting audio to the requested output path.
    /// </summary>
    /// <param name="request">Request containing the segment text, target output audio path, and optional voice preference.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A <see cref="TtsResult"/> describing the produced audio file and its length in bytes.</returns>
    /// <exception cref="ArgumentException">Thrown when <see cref="SingleSegmentTtsRequest.Text"/> is null, empty, or whitespace.</exception>
    public async Task<TtsResult> GenerateSegmentTtsAsync(
        SingleSegmentTtsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new ArgumentException("Segment text cannot be empty", nameof(request));

        var voiceName = ResolveVoiceName(request.VoiceName);
        var client = _clientLazy.Value;
        var audioBytes = await client.SynthesizeSpeechAsync(request.Text, voiceName, cancellationToken);

        var outputDir = Path.GetDirectoryName(request.OutputAudioPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        await File.WriteAllBytesAsync(request.OutputAudioPath, audioBytes, cancellationToken);

        _log.Info($"[GoogleCloudTTS] Generated segment audio: {request.OutputAudioPath} ({audioBytes.Length} bytes)");

        return new TtsResult(true, request.OutputAudioPath, request.VoiceName, audioBytes.Length, null);
    }

    private static string ResolveVoiceName(string modelOrVoice) => modelOrVoice switch
    {
        "standard" => "en-US-Standard-C",
        "wavenet" => "en-US-Wavenet-D",
        "neural2" => "en-US-Neural2-D",
        _ => "en-US-Standard-C"
    };
}
