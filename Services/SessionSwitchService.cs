using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Babel.Player.Models;

namespace Babel.Player.Services;

/// <summary>
/// Owns the persistence/cache mechanics around switching between workflow
/// sessions so the coordinator can focus on stage meaning instead of storage churn.
/// </summary>
public sealed class SessionSwitchService
{
    private readonly PerSessionSnapshotStore _perSessionStore;
    private readonly RecentSessionsStore _recentStore;
    private readonly AppLog _log;

    public SessionSwitchService(
        PerSessionSnapshotStore perSessionStore,
        RecentSessionsStore recentStore,
        AppLog log)
    {
        _perSessionStore = perSessionStore;
        _recentStore = recentStore;
        _log = log;
    }

    /// <summary>
    /// Persists the provided session, caches its media snapshot, updates the recent-sessions list, and returns the current recent sessions.
    /// </summary>
    /// <param name="currentSession">The session snapshot to persist and record as recent.</param>
    /// <param name="mediaSnapshotCache">A concurrent in-memory cache for media-keyed session snapshots where the snapshot will be stored.</param>
    /// <param name="cacheLimit">Maximum number of entries to keep in <paramref name="mediaSnapshotCache"/>; when exceeded the oldest cache entry is evicted.</param>
    /// <returns>The up-to-date list of recent session entries after saving and recording the provided session.</returns>
    public IReadOnlyList<RecentSessionEntry> StashCurrentSession(
        WorkflowSessionSnapshot currentSession,
        ConcurrentDictionary<string, WorkflowSessionSnapshot> mediaSnapshotCache,
        int cacheLimit)
    {
        if (string.IsNullOrWhiteSpace(currentSession.SourceMediaPath))
            return _recentStore.Load();

        CacheMediaSnapshot(
            mediaSnapshotCache,
            SessionWorkflowCoordinator.MediaKey(currentSession.SourceMediaPath),
            currentSession,
            cacheLimit);
        _perSessionStore.Save(currentSession);
        _recentStore.Upsert(new RecentSessionEntry(
            currentSession.SessionId,
            currentSession.SourceMediaPath,
            Path.GetFileName(currentSession.SourceMediaPath),
            currentSession.Stage,
            currentSession.LastUpdatedAtUtc));
        return _recentStore.Load();
    }

    public WorkflowSessionSnapshot? LoadSession(
        Guid sessionId,
        IReadOnlyDictionary<string, WorkflowSessionSnapshot> mediaSnapshotCache) =>
        mediaSnapshotCache.Values.FirstOrDefault(snapshot => snapshot.SessionId == sessionId)
        ?? _perSessionStore.Load(sessionId);

    public WorkflowSessionSnapshot? LoadSessionForMedia(
        string mediaPath,
        IReadOnlyDictionary<string, WorkflowSessionSnapshot> mediaSnapshotCache)
    {
        mediaSnapshotCache.TryGetValue(
            SessionWorkflowCoordinator.MediaKey(mediaPath),
            out var snapshot);
        return snapshot;
    }

    /// <summary>
    /// Stores the provided session snapshot in the media snapshot cache under the media-derived key and enforces the cache size limit.
    /// </summary>
    /// <param name="mediaPath">File path of the media used to derive the cache key.</param>
    /// <param name="snapshot">The session snapshot to place into the cache.</param>
    /// <param name="mediaSnapshotCache">The concurrent cache to update; the snapshot will be set at the media-derived key.</param>
    /// <param name="cacheLimit">Maximum number of entries to retain in the cache; if exceeded, the oldest entry will be evicted.</param>
    public void CacheCurrentSession(
        string mediaPath,
        WorkflowSessionSnapshot snapshot,
        ConcurrentDictionary<string, WorkflowSessionSnapshot> mediaSnapshotCache,
        int cacheLimit)
    {
        CacheMediaSnapshot(
            mediaSnapshotCache,
            SessionWorkflowCoordinator.MediaKey(mediaPath),
            snapshot,
            cacheLimit);
    }

    /// <summary>
        /// Get the filesystem directory path where the specified session's data is stored.
        /// </summary>
        /// <param name="sessionId">The GUID identifying the session.</param>
        /// <returns>The filesystem path to the session's directory.</returns>
        public string GetSessionDirectory(Guid sessionId) =>
        _perSessionStore.GetSessionDirectory(sessionId);

    /// <summary>
    /// Inserts or replaces a snapshot in the media cache under the specified key and, if the cache size exceeds the given limit, removes the oldest cached entry.
    /// </summary>
    /// <param name="cache">The concurrent dictionary that stores media-derived session snapshots.</param>
    /// <param name="key">The media-derived cache key to store the snapshot under.</param>
    /// <param name="snapshot">The session snapshot to cache.</param>
    /// <param name="cacheLimit">Maximum number of entries allowed in the cache; when exceeded, one oldest entry is evicted.</param>
    private void CacheMediaSnapshot(
        ConcurrentDictionary<string, WorkflowSessionSnapshot> cache,
        string key,
        WorkflowSessionSnapshot snapshot,
        int cacheLimit)
    {
        cache[key] = snapshot;
        if (cache.Count <= cacheLimit)
            return;

        var oldest = cache.Keys.FirstOrDefault();
        if (oldest != null && cache.TryRemove(oldest, out _))
        {
            _log.Info($"Evicted cached session for media key '{oldest}' to keep cache bounded.");
        }
    }
}
