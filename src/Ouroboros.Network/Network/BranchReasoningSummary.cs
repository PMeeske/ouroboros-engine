namespace Ouroboros.Network;

/// <summary>
/// Summary of reasoning steps in a pipeline branch.
/// </summary>
/// <param name="BranchName">Name of the branch.</param>
/// <param name="TotalSteps">Total number of reasoning steps.</param>
/// <param name="StepsByKind">Count of steps by kind (Draft, Critique, etc.).</param>
/// <param name="TotalToolCalls">Total number of tool calls across all steps.</param>
/// <param name="TotalDuration">Duration from first to last step.</param>
public sealed record BranchReasoningSummary(
    string BranchName,
    int TotalSteps,
    ImmutableDictionary<string, int> StepsByKind,
    int TotalToolCalls,
    TimeSpan TotalDuration);