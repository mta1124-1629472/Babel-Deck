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

    /// <summary>
    /// Reserves an active request slot for the specified request kind and returns a lease that releases that reservation when disposed.
    /// </summary>
    /// <param name="kind">The category of the request (for example: Qwen, Diarization, Transcription).</param>
    /// <returns>An <see cref="IDisposable"/> lease that, when disposed, releases the reserved request slot for the given kind.</returns>
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

    /// <summary>
    /// Releases a previously acquired request slot for the specified request kind by decrementing the total active request count and the corresponding kind-specific counter when applicable.
    /// </summary>
    /// <param name="kind">The category of the request whose counters should be decremented.</param>
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

        /// <summary>
        /// Releases the acquired request slot if it has not already been released.
        /// </summary>
        /// <remarks>
        /// Disposal is idempotent and thread-safe; the owner's <c>Release</c> is invoked at most once for this lease's request kind.
        /// </remarks>
        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.Release(kind);
        }
    }
}
