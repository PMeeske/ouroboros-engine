namespace Ouroboros.Agent.MetaAI.Affect;

/// <summary>
/// Queue statistics.
/// </summary>
public sealed record QueueStatistics(
    int TotalTasks,
    int PendingTasks,
    int InProgressTasks,
    int CompletedTasks,
    int FailedTasks,
    double AverageBasePriority,
    double AverageModulatedPriority,
    double HighestThreat,
    double HighestOpportunity);