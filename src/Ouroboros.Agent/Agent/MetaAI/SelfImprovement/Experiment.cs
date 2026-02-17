namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents an experiment designed to test a hypothesis.
/// </summary>
public sealed record Experiment(
    Guid Id,
    Hypothesis Hypothesis,
    string Description,
    List<PlanStep> Steps,
    Dictionary<string, object> ExpectedOutcomes,
    DateTime DesignedAt);