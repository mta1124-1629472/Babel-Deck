using System;

namespace Babel.Deck.Models;

public sealed record WorkflowSegmentState(
    string SegmentId,
    double StartSeconds,
    double EndSeconds,
    string SourceText,
    bool HasTranslation,
    string? TranslatedText,
    bool HasTtsAudio);
