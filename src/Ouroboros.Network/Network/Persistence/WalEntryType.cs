namespace Ouroboros.Network.Persistence;

/// <summary>
/// Represents the type of entry in the Write-Ahead Log.
/// </summary>
public enum WalEntryType
{
    /// <summary>
    /// Entry represents addition of a node to the DAG.
    /// </summary>
    AddNode,

    /// <summary>
    /// Entry represents addition of an edge to the DAG.
    /// </summary>
    AddEdge,
}