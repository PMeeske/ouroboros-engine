namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a concrete plan for executing an abstract task.
/// </summary>
public sealed record ConcretePlan(
    string AbstractTaskName,
    List<string> ConcreteSteps);