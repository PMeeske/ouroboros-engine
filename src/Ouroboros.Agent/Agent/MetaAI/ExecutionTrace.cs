namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents the execution trace of a plan including failure information.
/// </summary>
public sealed record ExecutionTrace(
    List<ExecutedStep> Steps,
    int FailedAtIndex,
    string FailureReason);