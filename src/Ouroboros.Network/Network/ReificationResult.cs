namespace Ouroboros.Network;

/// <summary>
/// Result of a branch reification operation.
/// </summary>
/// <param name="BranchName">Name of the reified branch.</param>
/// <param name="NodesCreated">Number of nodes created.</param>
/// <param name="TransitionsCreated">Number of transitions created.</param>
/// <param name="TotalNodes">Total nodes in the DAG.</param>
/// <param name="TotalTransitions">Total transitions in the DAG.</param>
public sealed record ReificationResult(
    string BranchName,
    int NodesCreated,
    int TransitionsCreated,
    int TotalNodes,
    int TotalTransitions);