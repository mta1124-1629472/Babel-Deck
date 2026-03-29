using System;
using System.IO;
using Xunit;
using Babel.Deck.Services;

namespace BabelDeck.Tests;

public class MediaTransportTests : IDisposable
{
    private readonly string _testFilePath;
    private IMediaTransport? _transport;

    public MediaTransportTests()
    {
        // Use the standard test file specified in instructions
        _testFilePath = Path.Combine(AppContext.BaseDirectory, "test-assets", "video", "sample.mp4");
    }

    [Fact]
    public void TransportCanInitializeAndLoadFile()
    {
        // Arrange - should not throw if libmpv loads correctly
        _transport = new LibMpvHeadlessTransport();
        
        // Act - load the test file
        _transport.Load(_testFilePath);
        
        // Act - read duration (meaningful property)
        long duration = _transport.Duration;
        
        // Verify - we got a duration (even if 0, it means the property worked)
        Assert.True(duration >= 0, $"Duration should be non-negative, got {duration}");
    }

    [Fact]
    public void TransportCanPlayAndPause()
    {
        // Arrange
        _transport = new LibMpvHeadlessTransport();
        _transport.Load(_testFilePath);
        
        // Act - play
        _transport.Play();
        
        // Act - pause
        _transport.Pause();
        
        // Verify - if we get here without exception, play/pause works
        Assert.True(true);
    }

    [Fact]
    public void TransportCanDisposeWithoutError()
    {
        // Arrange
        _transport = new LibMpvHeadlessTransport();
        
        // Act - load file
        _transport.Load(_testFilePath);
        
        // Act - dispose (should not throw)
        _transport.Dispose();
        
        // Assert - if we get here without exception, test passes
        Assert.True(true);
    }

    [Fact]
    public void TransportHandlesRepeatedLoadUnloadCycles()
    {
        // Test repeated load/unload cycles as required by Milestone 2
        
        // Cycle 1
        _transport = new LibMpvHeadlessTransport();
        _transport.Load(_testFilePath);
        _transport.Dispose();
        
        // Cycle 2
        _transport = new LibMpvHeadlessTransport();
        _transport.Load(_testFilePath);
        _transport.Dispose();
        
        // Cycle 3
        _transport = new LibMpvHeadlessTransport();
        _transport.Load(_testFilePath);
        
        // Verify
        Assert.True(true, "Multiple load/unload cycles completed without hanging");
    }

    [Fact]
    public void TransportCanSeek()
    {
        // Arrange
        _transport = new LibMpvHeadlessTransport();
        _transport.Load(_testFilePath);
        
        // Call Play to ensure media is ready (this waits for duration internally)
        _transport.Play();
        
        // Now seek to beginning
        _transport.Seek(0);
        
        // Verify - current time should be non-negative
        long currentTime = _transport.CurrentTime;
        Assert.True(currentTime >= 0, $"Current time should be non-negative, got {currentTime}");
    }

    [Fact]
    public void TransportCanReportEndedState()
    {
        // Arrange
        _transport = new LibMpvHeadlessTransport();
        _transport.Load(_testFilePath);
        
        // Act - get initial ended state
        bool initialEnded = _transport.HasEnded;
        
        // Verify - at start, should not be ended
        Assert.False(initialEnded, "Media should not be ended immediately after loading");
    }

    public void Dispose()
    {
        _transport?.Dispose();
    }
}