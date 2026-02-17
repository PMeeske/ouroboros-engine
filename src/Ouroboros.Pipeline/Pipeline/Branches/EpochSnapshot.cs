namespace Ouroboros.Pipeline.Branches;

/// <summary>
/// Represents an epoch snapshot of the global system state.
/// </summary>
public sealed record EpochSnapshot
{
    /// <summary>
    /// Gets the unique identifier for this epoch.
    /// </summary>
    public required Guid EpochId { get; init; }

    /// <summary>
    /// Gets the epoch number (incremental).
    /// </summary>
    public required long EpochNumber { get; init; }

    /// <summary>
    /// Gets the timestamp when this epoch was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets the collection of branch snapshots in this epoch.
    /// </summary>
    public required IReadOnlyList<BranchSnapshot> Branches { get; init; }

    /// <summary>
    /// Gets the metadata associated with this epoch.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets the hash of this epoch for integrity verification.
    /// </summary>
    public string? Hash { get; init; }
}