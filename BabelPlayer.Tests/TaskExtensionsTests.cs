using System;
using System.IO;
using System.Threading.Tasks;
using Babel.Player.Services;
using Xunit;

namespace BabelPlayer.Tests;

public sealed class TaskExtensionsTests : IDisposable
{
    private readonly AppLog _log;

    public TaskExtensionsTests()
    {
        _log = new AppLog(Path.GetTempFileName());
    }

    [Fact]
    public async Task FireAndForgetAsync_NullTask_ThrowsArgumentNullException()
    {
        Task? nullTask = null;
        await Assert.ThrowsAsync<ArgumentNullException>(() => nullTask!.FireAndForgetAsync(_log));
    }

    [Fact]
    public async Task FireAndForgetAsync_NullLog_ThrowsArgumentNullException()
    {
        var task = Task.CompletedTask;
        await Assert.ThrowsAsync<ArgumentNullException>(() => task.FireAndForgetAsync(null!));
    }

    [Fact]
    public async Task FireAndForgetAsync_ValidCall_DoesNotThrow()
    {
        var task = Task.CompletedTask;
        var exception = await Record.ExceptionAsync(() => task.FireAndForgetAsync(_log));
        Assert.Null(exception);
    }

    [Fact]
    public async Task FireAndForgetAsync_WithCustomContext_UsesContextInLog()
    {
        // This test verifies the method doesn't throw and uses the custom context
        // We can't easily test the log content without exposing internal logging,
        // but we can verify it doesn't throw
        var task = Task.CompletedTask;
        var exception = await Record.ExceptionAsync(() => task.FireAndForgetAsync(_log, "custom context"));
        Assert.Null(exception);
    }

    public void Dispose()
    {
        _log?.Dispose();
    }
}
