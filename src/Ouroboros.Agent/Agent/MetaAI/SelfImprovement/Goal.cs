namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a goal with hierarchical structure.
/// </summary>
public sealed record Goal(
    Guid Id,
    string Description,
    GoalType Type,
    double Priority,
    Goal? ParentGoal,
    List<Goal> Subgoals,
    Dictionary<string, object> Constraints,
    DateTime CreatedAt,
    bool IsComplete,
    string? CompletionReason)
{
    /// <summary>
    /// Creates a new goal with default values.
    /// </summary>
    public Goal(string description, GoalType type, double priority)
        : this(
            Guid.NewGuid(),
            description,
            type,
            priority,
            ParentGoal: null,
            Subgoals: new List<Goal>(),
            Constraints: new Dictionary<string, object>(),
            DateTime.UtcNow,
            IsComplete: false,
            CompletionReason: null)
    {
    }
}