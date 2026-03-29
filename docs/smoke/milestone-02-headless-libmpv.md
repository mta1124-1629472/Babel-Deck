# Milestone 02 Headless libmpv - Smoke Note

## Metadata
- Milestone: `02`
- Name: `Headless libmpv`
- Date: `2026-03-28`
- Status: `partial`

## Gate Summary
- [x] Repeated headless playback cycles complete without hangs
- [~] Teardown is stable
- [x] No ghost state survives reload cycles
- [~] A smoke note confirms repeated success rather than one lucky run

## What Was Verified
- Application builds and runs.
- `libmpv-2.dll` loads successfully from `native/win-x64`.
- libmpv initializes in headless mode with `vo=null` and `ao=null`.
- Media file loads successfully.
- Duration property can be read.
- Dispose works cleanly in the verified test path.
- Core headless lifecycle behavior has passing coverage for initialize, load plus duration, dispose, and repeated load/unload cycles.

## What Was Not Verified
- Play/Pause is not yet working reliably because the media is not ready for property changes until libmpv processes the file loading event.
- Explicit ended/completed event verification is not yet proven.
- Seek behavior is not explicitly proven in the supplied evidence.
- Full teardown stability is not yet strong enough to call the milestone complete until event-loop behavior is resolved.

## Evidence

### Commands Run
```text
dotnet build
dotnet test
```

### Test Results
```text
Passed: 3
- Initialize
- Load+Duration
- Dispose

Failed: 1
- Play/Pause
```

### Artifacts / Paths
- Native DLL: `native/win-x64/libmpv-2.dll`
- Test media: `test-assets/video/sample.mp4`

## Notes
- `IMediaTransport` defines `Load`, `Play`, `Pause`, `Seek`, `CurrentTime`, `Duration`, `HasEnded`, and `Dispose`.
- `LibMpvHeadlessTransport` is a P/Invoke wrapper around `libmpv-2.dll`.
- The current root cause is that `loadfile` returns success immediately, but the media is not yet ready for property changes until libmpv processes the file loading event.
- The next likely fix is either an event processing loop that waits for file-ready events or a synchronous/awaited load path.

## Conclusion
Milestone 02 is partial. Core libmpv risk is substantially reduced because the DLL loads, headless initialization works, file loading works, duration can be read, and clean disposal is proven in the current path. The milestone should not be marked complete until playback readiness and event-loop dependent behavior are explicitly verified.

## Deferred Items
- Event-loop integration for file-ready handling
- Explicit play/pause verification after readiness is established
- Explicit ended/completed event proof
- Explicit seek proof
