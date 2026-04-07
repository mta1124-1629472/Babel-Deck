using System.Collections.Generic;

namespace Babel.Player.Models;

/// <summary>
/// Result returned by <see cref="Babel.Player.Services.ITranscriptionProvider.TranscribeAsync"/>.
/// </summary>
/// <param name="Success">Whether transcription completed without error.</param>
/// <param name="Segments">Time-stamped transcript segments.</param>
/// <param name="Language">Detected language code (e.g. \"es\").</param>
/// <param name="LanguageProbability">Confidence of the detected language in [0,1].</param>
/// <param name="ErrorMessage">Non-null when <see cref="Success"/> is false.</param>
/// <param name="ElapsedMs">
/// Wall-clock inference duration in milliseconds, sourced from
/// <see cref="Babel.Player.Services.PythonSubprocessServiceBase"/>.<c>ScriptResult.ElapsedMs</c>.
/// 0 when not available (e.g. cloud providers that do not time internally).
/// </param>
/// <param name="PeakVramMb">
/// Peak GPU VRAM in MB sampled during inference. -1 when unavailable.
/// Populated by local providers that embed pynvml sampling in their Python script.
/// </param>
/// <param name="PeakRamMb">
/// Peak CPU RAM in MB sampled during inference. -1 when unavailable.
/// Populated by local providers that embed psutil sampling in their Python script.
/// </param>
public sealed record TranscriptionResult(
    bool Success,
    IReadOnlyList<Babel.Player.Services.TranscriptSegment>? Segments,
    string Language,
    double LanguageProbability,
    string? ErrorMessage,
    long ElapsedMs = 0,
    double PeakVramMb = -1,
    double PeakRamMb = -1
);
