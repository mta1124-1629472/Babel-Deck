using System;
using System.IO;
using System.Threading.Tasks;
using Babel.Deck.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Babel.Deck.Services;

public sealed partial class SessionWorkflowCoordinator : ObservableObject
{
    private readonly SessionSnapshotStore _store;
    private readonly AppLog _log;
    private TranscriptionService? _transcriptionService;
    private TranslationService? _translationService;

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
            bool mediaMissing = false;
            bool transcriptMissing = false;
            bool translationMissing = false;

            if (!string.IsNullOrEmpty(snapshot.IngestedMediaPath) && 
                !File.Exists(snapshot.IngestedMediaPath))
            {
                _log.Warning($"Ingested media artifact missing: {snapshot.IngestedMediaPath}");
                mediaMissing = true;
            }

            if (!string.IsNullOrEmpty(snapshot.TranscriptPath) && 
                !File.Exists(snapshot.TranscriptPath))
            {
                _log.Warning($"Transcript artifact missing: {snapshot.TranscriptPath}");
                transcriptMissing = true;
            }

            if (!string.IsNullOrEmpty(snapshot.TranslationPath) && 
                !File.Exists(snapshot.TranslationPath))
            {
                _log.Warning($"Translation artifact missing: {snapshot.TranslationPath}");
                translationMissing = true;
            }

            string statusMessage;
            if (mediaMissing && transcriptMissing && translationMissing)
            {
                statusMessage = "Session had all artifacts but they are missing. Please restart the workflow.";
            }
            else if (translationMissing && snapshot.Stage >= SessionWorkflowStage.Translated)
            {
                statusMessage = "Session had translation but artifact is missing. Please re-translate.";
            }
            else if (transcriptMissing && snapshot.Stage >= SessionWorkflowStage.Transcribed)
            {
                statusMessage = "Session had transcript but artifact is missing. Please re-transcribe.";
            }
            else if (mediaMissing)
            {
                statusMessage = "Session had media but artifact is missing. Please re-load media.";
            }
            else
            {
                statusMessage = snapshot.Stage >= SessionWorkflowStage.Translated
                    ? "Resumed session with translation. Ready for TTS/dubbing."
                    : snapshot.Stage >= SessionWorkflowStage.Transcribed
                        ? "Resumed session with transcript. Ready for translation."
                        : "Resumed saved foundation session. Downstream workflow milestones are still not implemented.";
            }

            CurrentSession = snapshot with
            {
                LastUpdatedAtUtc = nowUtc,
                StatusMessage = statusMessage,
            };

            if (mediaMissing && transcriptMissing && translationMissing)
            {
                SessionSource = "Resumed session but all artifacts are missing.";
            }
            else if (translationMissing && snapshot.Stage >= SessionWorkflowStage.Translated)
            {
                SessionSource = "Resumed session but translation artifact is missing.";
            }
            else if (transcriptMissing && snapshot.Stage >= SessionWorkflowStage.Transcribed)
            {
                SessionSource = "Resumed session but transcript artifact is missing.";
            }
            else if (mediaMissing)
            {
                SessionSource = "Resumed session but media artifact is missing.";
            }
            else
            {
                SessionSource = snapshot.Stage >= SessionWorkflowStage.Translated
                    ? "Resumed session with translation."
                    : snapshot.Stage >= SessionWorkflowStage.Transcribed
                        ? "Resumed session with transcript."
                        : "Resumed the saved foundation session.";
            }
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

    public async Task TranscribeMediaAsync()
    {
        if (string.IsNullOrEmpty(CurrentSession.IngestedMediaPath))
        {
            throw new InvalidOperationException("No media loaded. Please load media first.");
        }

        if (!File.Exists(CurrentSession.IngestedMediaPath))
        {
            throw new FileNotFoundException($"Ingested media file not found: {CurrentSession.IngestedMediaPath}");
        }

        _transcriptionService ??= new TranscriptionService(_log);

        var sessionDir = GetSessionDirectory();
        var transcriptDir = Path.Combine(sessionDir, "transcripts");
        Directory.CreateDirectory(transcriptDir);

        var fileName = Path.GetFileNameWithoutExtension(CurrentSession.IngestedMediaPath);
        var transcriptPath = Path.Combine(transcriptDir, $"{fileName}.json");

        _log.Info($"Starting transcription: {CurrentSession.IngestedMediaPath}");

        var result = await _transcriptionService.TranscribeAsync(
            CurrentSession.IngestedMediaPath, 
            transcriptPath);

        if (!result.Success)
        {
            var errorMsg = result.ErrorMessage ?? "Unknown transcription error";
            _log.Error($"Transcription failed: {errorMsg}", new Exception(errorMsg));
            throw new InvalidOperationException($"Transcription failed: {errorMsg}");
        }

        var nowUtc = DateTimeOffset.UtcNow;
        CurrentSession = CurrentSession with
        {
            Stage = SessionWorkflowStage.Transcribed,
            TranscriptPath = transcriptPath,
            TranscribedAtUtc = nowUtc,
            StatusMessage = $"Transcribed {result.Segments.Count} segments. Ready for translation.",
        };

        _log.Info($"Transcription complete: {result.Segments.Count} segments, language: {result.Language}");
        SaveCurrentSession();
    }

    public async Task TranslateTranscriptAsync(string targetLanguage = "en", string sourceLanguage = "es")
    {
        if (string.IsNullOrEmpty(CurrentSession.TranscriptPath))
        {
            throw new InvalidOperationException("No transcript available. Please transcribe media first.");
        }

        if (!File.Exists(CurrentSession.TranscriptPath))
        {
            throw new FileNotFoundException($"Transcript file not found: {CurrentSession.TranscriptPath}");
        }

        _translationService ??= new TranslationService(_log);

        var sessionDir = GetSessionDirectory();
        var translationDir = Path.Combine(sessionDir, "translations");
        Directory.CreateDirectory(translationDir);

        var fileName = Path.GetFileNameWithoutExtension(CurrentSession.TranscriptPath);
        var translationPath = Path.Combine(translationDir, $"{fileName}_{targetLanguage}.json");

        _log.Info($"Starting translation: {CurrentSession.TranscriptPath} ({sourceLanguage} -> {targetLanguage})");

        var result = await _translationService.TranslateAsync(
            CurrentSession.TranscriptPath,
            translationPath,
            sourceLanguage,
            targetLanguage);

        if (!result.Success)
        {
            var errorMsg = result.ErrorMessage ?? "Unknown translation error";
            _log.Error($"Translation failed: {errorMsg}", new Exception(errorMsg));
            throw new InvalidOperationException($"Translation failed: {errorMsg}");
        }

        var nowUtc = DateTimeOffset.UtcNow;
        CurrentSession = CurrentSession with
        {
            Stage = SessionWorkflowStage.Translated,
            TranslationPath = translationPath,
            TargetLanguage = targetLanguage,
            TranslatedAtUtc = nowUtc,
            StatusMessage = $"Translated {result.Segments.Count} segments to {targetLanguage}. Ready for TTS/dubbing.",
        };

        _log.Info($"Translation complete: {result.Segments.Count} segments, {sourceLanguage} -> {targetLanguage}");
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
