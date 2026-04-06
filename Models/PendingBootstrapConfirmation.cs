using System;

namespace Babel.Player.Models;

/// <summary>
/// Represents a pending bootstrap confirmation that requires user action before proceeding.
/// Used to show size warnings before first-time downloads.
/// </summary>
public record PendingBootstrapConfirmation(
    string Label,          // "GPU inference runtime" or "CPU inference runtime"
    string SizeEstimate,   // "~5 GB" or "~800 MB"
    Action OnConfirm,
    Action OnDismiss);
