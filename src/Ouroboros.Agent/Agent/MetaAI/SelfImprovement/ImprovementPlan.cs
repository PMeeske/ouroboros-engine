namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a plan for self-improvement.
/// </summary>
public sealed record ImprovementPlan(
    string Goal,
    List<string> Actions,
    Dictionary<string, double> ExpectedImprovements,
    TimeSpan EstimatedDuration,
    double Priority,
    DateTime CreatedAt);