namespace Ouroboros.Pipeline.GraphRAG.Models;

/// <summary>
/// Represents a step in the reasoning chain.
/// </summary>
/// <param name="StepNumber">The step number in the chain.</param>
/// <param name="Operation">The operation performed (e.g., "Traverse", "Match", "Infer").</param>
/// <param name="Description">Human-readable description of the step.</param>
/// <param name="EntitiesInvolved">Entity IDs involved in this step.</param>
public sealed record ReasoningChainStep(
    int StepNumber,
    string Operation,
    string Description,
    IReadOnlyList<string> EntitiesInvolved);