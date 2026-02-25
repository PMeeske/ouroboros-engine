namespace Ouroboros.Pipeline.Branches;

/// <summary>
/// Represents a snapshot with metadata for retention evaluation.
/// </summary>
public sealed record SnapshotMetadata
{
    /// <summary>
    /// Gets the snapshot identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the branch name.
    /// </summary>
    public required string BranchName { get; init; }

    /// <summary>
    /// Gets the timestamp when the snapshot was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets the snapshot hash for integrity verification.
    /// </summary>
    public required string Hash { get; init; }

    /// <summary>
    /// Gets the size of the snapshot in bytes.
    /// </summary>
    public long SizeBytes { get; init; }
}