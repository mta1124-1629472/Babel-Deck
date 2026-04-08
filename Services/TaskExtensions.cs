using System;
using System.Threading.Tasks;

namespace Babel.Player.Services;

internal static class TaskExtensions
{
    /// <summary>
    /// Safely executes a task in the background without awaiting it.
    /// Any unhandled exceptions are caught and explicitly routed to the application log.
    /// </summary>
    public static void FireAndForgetAsync(this Task task, AppLog log, string context = "background operation")
    {
        task.ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception != null)
            {
                log.Error($"Unhandled exception during {context}", t.Exception);
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}
