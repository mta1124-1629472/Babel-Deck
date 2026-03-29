using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Babel.Deck.Models;
using Babel.Deck.Services;

namespace BabelDeck.Tests;

public class SessionWorkflowTests : IDisposable
{
    private readonly string _testStateDir;
    private readonly string _testLogDir;
    private readonly string _testMediaPath;
    private string? _lastStateFilePath;

    public SessionWorkflowTests()
    {
        _testStateDir = Path.Combine(Path.GetTempPath(), $"BabelDeckTest_{Guid.NewGuid():N}");
        _testLogDir = Path.Combine(Path.GetTempPath(), $"BabelDeckLogTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testStateDir);
        Directory.CreateDirectory(_testLogDir);
        
        var mp4Path = Path.Combine(AppContext.BaseDirectory, "test-assets", "video", "sample.mp4");
        var wavPath = Path.Combine(AppContext.BaseDirectory, "test-assets", "audio", "sample.wav");
        
        if (File.Exists(wavPath))
            _testMediaPath = wavPath;
        else
            _testMediaPath = mp4Path;
    }

    private string GetTestLogPath() => Path.Combine(_testLogDir, "test.log");

    [Fact]
    public void LoadMedia_ThenReopen_ReusesArtifact()
    {
        var stateFilePath = Path.Combine(_testStateDir, "session.json");
        _lastStateFilePath = stateFilePath;

        var log = new AppLog(GetTestLogPath());
        var store = new SessionSnapshotStore(stateFilePath, log);

        var coordinator = new SessionWorkflowCoordinator(store, log);

        coordinator.Initialize();
        Assert.Equal(SessionWorkflowStage.Foundation, coordinator.CurrentSession.Stage);

        Assert.True(File.Exists(_testMediaPath), $"Test media not found: {_testMediaPath}");
        coordinator.LoadMedia(_testMediaPath);

        Assert.Equal(SessionWorkflowStage.MediaLoaded, coordinator.CurrentSession.Stage);
        Assert.NotNull(coordinator.CurrentSession.SourceMediaPath);
        Assert.NotNull(coordinator.CurrentSession.IngestedMediaPath);
        Assert.Equal(_testMediaPath, coordinator.CurrentSession.SourceMediaPath);
        Assert.True(File.Exists(coordinator.CurrentSession.IngestedMediaPath), 
            $"Ingested media should exist at: {coordinator.CurrentSession.IngestedMediaPath}");

        var sessionId = coordinator.CurrentSession.SessionId;

        coordinator = new SessionWorkflowCoordinator(store, log);
        coordinator.Initialize();

        Assert.Equal(sessionId, coordinator.CurrentSession.SessionId);
        Assert.Equal(SessionWorkflowStage.MediaLoaded, coordinator.CurrentSession.Stage);
        Assert.NotNull(coordinator.CurrentSession.IngestedMediaPath);
        Assert.True(File.Exists(coordinator.CurrentSession.IngestedMediaPath),
            "After reopen, ingested media artifact should still exist");
        Assert.DoesNotContain("missing", coordinator.CurrentSession.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReopenWithMissingArtifact_SurfacesDegradedState()
    {
        var stateFilePath = Path.Combine(_testStateDir, "session_missing_artifact.json");
        _lastStateFilePath = stateFilePath;

        var log = new AppLog(GetTestLogPath());
        var store = new SessionSnapshotStore(stateFilePath, log);

        var coordinator = new SessionWorkflowCoordinator(store, log);
        coordinator.Initialize();

        Assert.True(File.Exists(_testMediaPath));
        coordinator.LoadMedia(_testMediaPath);

        var ingestedPath = coordinator.CurrentSession.IngestedMediaPath;
        Assert.NotNull(ingestedPath);

        File.Delete(ingestedPath);

        coordinator = new SessionWorkflowCoordinator(store, log);
        coordinator.Initialize();

        Assert.Contains("missing", coordinator.CurrentSession.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TranscribeMedia_ProducesTimedSegments()
    {
        var stateFilePath = Path.Combine(_testStateDir, "session_transcribe.json");
        _lastStateFilePath = stateFilePath;

        var log = new AppLog(GetTestLogPath());
        var store = new SessionSnapshotStore(stateFilePath, log);

        var coordinator = new SessionWorkflowCoordinator(store, log);
        coordinator.Initialize();

        Assert.True(File.Exists(_testMediaPath), $"Test media not found: {_testMediaPath}");
        coordinator.LoadMedia(_testMediaPath);

        await coordinator.TranscribeMediaAsync();

        Assert.Equal(SessionWorkflowStage.Transcribed, coordinator.CurrentSession.Stage);
        Assert.NotNull(coordinator.CurrentSession.TranscriptPath);
        Assert.True(File.Exists(coordinator.CurrentSession.TranscriptPath), 
            $"Transcript should exist at: {coordinator.CurrentSession.TranscriptPath}");

        var transcriptJson = await File.ReadAllTextAsync(coordinator.CurrentSession.TranscriptPath);
        Assert.NotEmpty(transcriptJson);
        Assert.Contains("segments", transcriptJson);
    }

    [Fact]
    public async Task TranscribeMedia_ThenReopen_ReusesTranscript()
    {
        var stateFilePath = Path.Combine(_testStateDir, "session_transcribe_reopen.json");
        _lastStateFilePath = stateFilePath;

        var log = new AppLog(GetTestLogPath());
        var store = new SessionSnapshotStore(stateFilePath, log);

        var coordinator = new SessionWorkflowCoordinator(store, log);
        coordinator.Initialize();

        coordinator.LoadMedia(_testMediaPath);
        await coordinator.TranscribeMediaAsync();

        var transcriptPath = coordinator.CurrentSession.TranscriptPath;
        var sessionId = coordinator.CurrentSession.SessionId;

        Assert.NotNull(transcriptPath);
        Assert.True(File.Exists(transcriptPath));

        coordinator = new SessionWorkflowCoordinator(store, log);
        coordinator.Initialize();

        Assert.Equal(sessionId, coordinator.CurrentSession.SessionId);
        Assert.Equal(SessionWorkflowStage.Transcribed, coordinator.CurrentSession.Stage);
        Assert.NotNull(coordinator.CurrentSession.TranscriptPath);
        Assert.True(File.Exists(coordinator.CurrentSession.TranscriptPath),
            "After reopen, transcript artifact should still exist");
        Assert.DoesNotContain("missing", coordinator.CurrentSession.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReopenWithMissingTranscript_SurfacesDegradedState()
    {
        var stateFilePath = Path.Combine(_testStateDir, "session_missing_transcript.json");
        _lastStateFilePath = stateFilePath;

        var log = new AppLog(GetTestLogPath());
        var store = new SessionSnapshotStore(stateFilePath, log);

        var coordinator = new SessionWorkflowCoordinator(store, log);
        coordinator.Initialize();

        coordinator.LoadMedia(_testMediaPath);
        await coordinator.TranscribeMediaAsync();

        var transcriptPath = coordinator.CurrentSession.TranscriptPath;
        Assert.NotNull(transcriptPath);

        File.Delete(transcriptPath);

        coordinator = new SessionWorkflowCoordinator(store, log);
        coordinator.Initialize();

        Assert.Contains("missing", coordinator.CurrentSession.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testStateDir))
        {
            Directory.Delete(_testStateDir, true);
        }
        if (Directory.Exists(_testLogDir))
        {
            Directory.Delete(_testLogDir, true);
        }
    }
}
