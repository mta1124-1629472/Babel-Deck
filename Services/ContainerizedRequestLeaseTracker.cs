using System;
using System.Threading;

namespace Babel.Player.Services;

public enum ContainerizedRequestKind
{
    Other,
    Transcription,
    Translation,
    Tts,
    Qwen,
    Diarization,
}

/// <summary>
/// Tracks in-flight requests against the managed inference host so lifecycle code
/// can avoid restarting the host while local work is still active.
/// </summary>
public sealed class ContainerizedRequestLeaseTracker
{
    private int _activeRequests;
    private int _activeQwenRequests;
    private int _activeDiarizationRequests;

    public int ActiveRequests => Volatile.Read(ref _activeRequests);

    public int ActiveQwenRequests => Volatile.Read(ref _activeQwenRequests);

    public int ActiveDiarizationRequests => Volatile.Read(ref _activeDiarizationRequests);

    public bool HasActiveRequests => ActiveRequests > 0;

    public IDisposable Acquire(ContainerizedRequestKind kind)
    {
        Interlocked.Increment(ref _activeRequests);

        switch (kind)
        {
            case ContainerizedRequestKind.Qwen:
                Interlocked.Increment(ref _activeQwenRequests);
                break;
            case ContainerizedRequestKind.Diarization:
                Interlocked.Increment(ref _activeDiarizationRequests);
                break;
        }

        return new Lease(this, kind);
    }

    private void Release(ContainerizedRequestKind kind)
    {
        switch (kind)
        {
            case ContainerizedRequestKind.Qwen:
                Interlocked.Decrement(ref _activeQwenRequests);
                break;
            case ContainerizedRequestKind.Diarization:
                Interlocked.Decrement(ref _activeDiarizationRequests);
                break;
        }

        Interlocked.Decrement(ref _activeRequests);
    }

    private sealed class Lease(ContainerizedRequestLeaseTracker owner, ContainerizedRequestKind kind) : IDisposable
    {
        private ContainerizedRequestLeaseTracker? _owner = owner;

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.Release(kind);
        }
    }
}
