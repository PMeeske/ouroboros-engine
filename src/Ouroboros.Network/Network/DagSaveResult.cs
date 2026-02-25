namespace Ouroboros.Network;

/// <summary>
/// Result of saving a DAG to Qdrant.
/// </summary>
/// <param name="NodesSaved">Number of nodes saved.</param>
/// <param name="EdgesSaved">Number of edges saved.</param>
/// <param name="Errors">Any errors encountered.</param>
public sealed record DagSaveResult(int NodesSaved, int EdgesSaved, IReadOnlyList<string> Errors);