namespace Ouroboros.Providers;

/// <summary>
/// Result of executing a sub-goal.
/// </summary>
public sealed record SubGoalResult(
    string GoalId,
    string PathwayUsed,
    ThinkingResponse Response,
    TimeSpan Duration,
    bool Success,
    string? ErrorMessage = null);