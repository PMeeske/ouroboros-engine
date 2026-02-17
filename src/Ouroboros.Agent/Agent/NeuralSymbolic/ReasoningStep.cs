namespace Ouroboros.Agent.NeuralSymbolic;

/// <summary>
/// A step in the reasoning process.
/// </summary>
public sealed record ReasoningStep(
    int StepNumber,
    string Description,
    string RuleApplied,
    ReasoningStepType Type);