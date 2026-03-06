using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Cognition.Planning;

/// <summary>
/// The result of a goal split: a tree of sub-goals annotated with
/// Hypergrid dimensional metadata for routing.
/// </summary>
public sealed record GoalDecomposition(
    Goal OriginalGoal,
    IReadOnlyList<GoalStep> Steps,
    HypergridAnalysis DimensionalAnalysis,
    DateTimeOffset CreatedAt)
{
    public GoalDecomposition(Goal goal, IReadOnlyList<GoalStep> steps, HypergridAnalysis analysis)
        : this(goal, steps, analysis, DateTimeOffset.UtcNow) { }
}

/// <summary>
/// A single step in a decomposed goal plan, annotated with
/// dimensional routing information.
/// </summary>
public sealed record GoalStep(
    Guid Id,
    string Description,
    GoalType Type,
    double Priority,
    DimensionalCoordinate Coordinate,
    IReadOnlyList<Guid> DependsOn,
    ExecutionMode Mode)
{
    public GoalStep(string description, GoalType type, double priority, DimensionalCoordinate coordinate)
        : this(Guid.NewGuid(), description, type, priority, coordinate, Array.Empty<Guid>(), ExecutionMode.Automatic) { }

    /// <summary>Converts this step to a Goal record for the existing hierarchy.</summary>
    public Goal ToGoal(Goal? parent = null)
        => new(Id, Description, Type, Priority, parent, new List<Goal>(),
               new Dictionary<string, object>
               {
                   ["temporal"] = Coordinate.Temporal,
                   ["semantic"] = Coordinate.Semantic,
                   ["causal"] = Coordinate.Causal,
                   ["modal"] = Coordinate.Modal,
                   ["executionMode"] = Mode.ToString()
               },
               DateTime.UtcNow, false, null);
}

/// <summary>
/// How a goal step should be executed.
/// Maps to Hypergrid modal axis (Section 7.2 d3).
/// </summary>
public enum ExecutionMode
{
    /// <summary>Automatic execution by the agent.</summary>
    Automatic,
    /// <summary>Requires human approval before execution.</summary>
    RequiresApproval,
    /// <summary>Delegate to an external tool or API.</summary>
    ToolDelegation,
    /// <summary>Delegate to a human operator.</summary>
    HumanDelegation
}
