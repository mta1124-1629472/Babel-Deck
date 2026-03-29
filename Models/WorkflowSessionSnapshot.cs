using System;

namespace Babel.Deck.Models;

public sealed record WorkflowSessionSnapshot(
    Guid SessionId,
    SessionWorkflowStage Stage,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastUpdatedAtUtc,
    string StatusMessage,
    string? SourceMediaPath = null,
    string? IngestedMediaPath = null,
    DateTimeOffset? MediaLoadedAtUtc = null)
{
    public static WorkflowSessionSnapshot CreateNew(DateTimeOffset nowUtc)
    {
        return new WorkflowSessionSnapshot(
            Guid.NewGuid(),
            SessionWorkflowStage.Foundation,
            nowUtc,
            nowUtc,
            "Foundation ready. Media ingest, transcription, translation, and dubbing are not implemented yet.");
    }
}
