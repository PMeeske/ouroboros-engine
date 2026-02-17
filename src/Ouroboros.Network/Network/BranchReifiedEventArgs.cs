namespace Ouroboros.Network;

/// <summary>
/// Event args for branch reification events.
/// </summary>
public sealed class BranchReifiedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BranchReifiedEventArgs"/> class.
    /// </summary>
    public BranchReifiedEventArgs(string branchName, int nodesCreated)
    {
        this.BranchName = branchName;
        this.NodesCreated = nodesCreated;
        this.Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the name of the reified branch.
    /// </summary>
    public string BranchName { get; }

    /// <summary>
    /// Gets the number of nodes created.
    /// </summary>
    public int NodesCreated { get; }

    /// <summary>
    /// Gets the timestamp of the reification.
    /// </summary>
    public DateTime Timestamp { get; }
}