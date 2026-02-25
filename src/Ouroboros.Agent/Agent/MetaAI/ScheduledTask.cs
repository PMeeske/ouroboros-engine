namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a task scheduled with specific start and end times.
/// </summary>
public sealed record ScheduledTask(
    string Name,
    DateTime StartTime,
    DateTime EndTime,
    List<string> Dependencies);