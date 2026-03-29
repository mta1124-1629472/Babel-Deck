using System;
using System.IO;
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
        
        _testMediaPath = Path.Combine(AppContext.BaseDirectory, "test-assets", "video", "sample.mp4");
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
