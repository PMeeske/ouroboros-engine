namespace Ouroboros.Providers;

/// <summary>
/// Election event for observability.
/// </summary>
public sealed record ElectionEvent(
    ElectionEventType Type,
    string Message,
    DateTime Timestamp,
    string? Winner = null,
    IReadOnlyDictionary<string, double>? Votes = null);