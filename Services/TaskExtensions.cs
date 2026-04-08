using System;
using System.Threading.Tasks;

namespace Babel.Player.Services;

internal static class TaskExtensions
{
    /// <summary>
    /// Safely executes a task in the background without awaiting it.
    /// Any unhandled exceptions are caught and explicitly routed to the application log.
    /// <summary>
    /// Starts the given task without awaiting it and ensures any unhandled exception is logged.
    /// </summary>
    /// <param name="task">The task to run in the background.</param>
    /// <param name="log">Logger used to record unhandled exceptions.</param>
    /// <param name="context">Description included in the log message to identify the task's context.</param>
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
