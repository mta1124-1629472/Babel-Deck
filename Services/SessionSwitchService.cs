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

    public string GetSessionDirectory(Guid sessionId) =>
        _perSessionStore.GetSessionDirectory(sessionId);

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
