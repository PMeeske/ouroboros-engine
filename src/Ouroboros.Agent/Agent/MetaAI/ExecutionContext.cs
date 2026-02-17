namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents execution context for adaptation decisions.
/// </summary>
public sealed record ExecutionContext(
    Plan OriginalPlan,
    List<StepResult> CompletedSteps,
    PlanStep CurrentStep,
    int CurrentStepIndex,
    Dictionary<string, object> Metadata);