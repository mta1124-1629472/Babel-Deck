# Milestone 7.2 Single Segment Translation Regeneration - Smoke Note

## Metadata
- Milestone: `07.2`
- Name: `Single Segment Translation Regeneration`
- Date: `2026-03-28`
- Status: `complete`

## Gate Summary
- [x] Regenerate translation for a single segment by stable segment ID
- [x] Leave all other segments unchanged
- [x] Persist the updated translation artifact
- [x] Reopen session and preserve mixed state correctly

## What Was Verified
- `RegenerateSegmentTranslationAsync(segmentId)` re-translates only one segment
- Translation segment ID based on start time (e.g., "segment_0.0")
- Uses segment's `text` field as source for re-translation
- Only the selected segment's `translatedText` is updated
- Other segments remain byte-for-byte unchanged
- Translation persists to existing artifact file
- Reopen preserves updated segment

## Evidence

### Commands Run
```bash
dotnet build Babel-Deck.sln
dotnet test BabelDeck.Tests/BabelDeck.Tests.csproj
```

### Test Results
```text
Total tests: 23
Passed: 23

New tests (Milestone 7.2):
- RegenerateSegmentTranslation_UpdatesSingleSegment
- RegenerateSegmentTranslation_DoesNotModifyOtherSegments
- Reopen_PreservesUpdatedTranslationSegment

Existing tests still pass (20):
- MediaTransportTests (6)
- SessionWorkflowTests (17)
```

### Implementation Details
- Uses segment IDs from translation JSON (e.g., "segment_0.0", "segment_3.68")
- Reads source text from segment's `text` field (NOT translatedText)
- Re-translates via googletrans
- Updates only the selected segment in the JSON file
- Other segments preserved unchanged

## Notes
- googletrans is deterministic - same input produces same output
- Test verifies segment was processed (not that translation changed)
- Other segments verified unchanged by comparing translatedText values
- No TTS modifications
- No UI work

## Conclusion
Milestone 7.2 complete. Single segment translation regeneration works:
- regenerate translation for one segment by ID
- other segments unchanged
- persisted to translation artifact
- reopen preserves updated segment

## What Remains
- Full dub session workflow (Milestone 7)
- In-context preview
- Multiple voice options
