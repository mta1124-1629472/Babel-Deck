using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Babel.Player.Models;

public sealed class TranscriptArtifact
{
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("language_probability")]
    public double LanguageProbability { get; set; }

    [JsonPropertyName("segments")]
    public List<TranscriptSegmentArtifact>? Segments { get; set; }
}

public sealed class TranscriptSegmentArtifact
{
    [JsonPropertyName("start")]
    public double Start { get; set; }

    [JsonPropertyName("end")]
    public double End { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("speakerId")]
    public string? SpeakerId { get; set; }

    [JsonPropertyName("words")]
    public List<WordTimestamp>? Words { get; set; }

    /// <summary>
    /// Set only on segments produced by speaker-boundary splitting.
    /// Preserves the original segment's Start so downstream artifacts
    /// (translation, TTS) keyed by that time can still be aligned.
    /// </summary>
    [JsonPropertyName("originalStart")]
    public double? OriginalStart { get; set; }
}

public sealed record WordTimestamp(
    [property: JsonPropertyName("text")]  string Text,
    [property: JsonPropertyName("start")] double Start,
    [property: JsonPropertyName("end")]   double End);
