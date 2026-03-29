using System;
using System.IO;
using Babel.Deck.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Babel.Deck.Services;

public sealed partial class SessionWorkflowCoordinator : ObservableObject
{
    private readonly SessionSnapshotStore _store;
    private readonly AppLog _log;

    [ObservableProperty]
    private WorkflowSessionSnapshot _currentSession = WorkflowSessionSnapshot.CreateNew(DateTimeOffset.UtcNow);

    [ObservableProperty]
    private string _sessionSource = "Session not initialized.";

    [ObservableProperty]
    private string _persistenceStatus = "Persistence has not run yet.";

    public SessionWorkflowCoordinator(SessionSnapshotStore store, AppLog log)
    {
        _store = store;
        _log = log;
    }

    public string StateFilePath => _store.StateFilePath;

    public string LogFilePath => _log.LogFilePath;

    public void Initialize()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var loadResult = _store.Load();

        if (loadResult.Snapshot is null)
        {
            CurrentSession = WorkflowSessionSnapshot.CreateNew(nowUtc);
            SessionSource = "Created a new foundation session.";
        }
        else
        {
            var snapshot = loadResult.Snapshot;
            bool artifactMissing = false;

            if (!string.IsNullOrEmpty(snapshot.IngestedMediaPath) && 
                !File.Exists(snapshot.IngestedMediaPath))
            {
                _log.Warning($"Ingested media artifact missing: {snapshot.IngestedMediaPath}");
                artifactMissing = true;
            }

            CurrentSession = snapshot with
            {
                LastUpdatedAtUtc = nowUtc,
                StatusMessage = artifactMissing
                    ? "Session had media but artifact is missing. Please re-load media."
                    : "Resumed saved foundation session. Downstream workflow milestones are still not implemented.",
            };
            SessionSource = artifactMissing
                ? "Resumed session but media artifact is missing."
                : "Resumed the saved foundation session.";
        }

        PersistenceStatus = loadResult.StatusMessage;
        _log.Info(SessionSource);
        SaveCurrentSession();
    }

    public void LoadMedia(string sourceMediaPath)
    {
        if (!File.Exists(sourceMediaPath))
        {
            throw new FileNotFoundException($"Source media file not found: {sourceMediaPath}");
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var sessionDir = GetSessionDirectory();
        var mediaDir = Path.Combine(sessionDir, "media");
        Directory.CreateDirectory(mediaDir);

        var fileName = Path.GetFileName(sourceMediaPath);
        var ingestedPath = Path.Combine(mediaDir, fileName);

        File.Copy(sourceMediaPath, ingestedPath, overwrite: true);
        _log.Info($"Copied media to session artifact: {ingestedPath}");

        CurrentSession = CurrentSession with
        {
            Stage = SessionWorkflowStage.MediaLoaded,
            SourceMediaPath = sourceMediaPath,
            IngestedMediaPath = ingestedPath,
            MediaLoadedAtUtc = nowUtc,
            StatusMessage = "Media loaded. Ready for transcription.",
        };

        SaveCurrentSession();
    }

    private string GetSessionDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "BabelDeck", "sessions", CurrentSession.SessionId.ToString());
    }

    public void SaveCurrentSession()
    {
        CurrentSession = CurrentSession with { LastUpdatedAtUtc = DateTimeOffset.UtcNow };
        _store.Save(CurrentSession);
        PersistenceStatus = $"Saved current session snapshot to {StateFilePath}.";
        _log.Info(PersistenceStatus);
    }
}
