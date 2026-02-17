namespace Ouroboros.Pipeline.Branches;

/// <summary>
/// Result of a retention plan evaluation.
/// </summary>
public sealed record RetentionPlan
{
    /// <summary>
    /// Gets the snapshots to keep.
    /// </summary>
    public required IReadOnlyList<SnapshotMetadata> ToKeep { get; init; }

    /// <summary>
    /// Gets the snapshots to delete.
    /// </summary>
    public required IReadOnlyList<SnapshotMetadata> ToDelete { get; init; }

    /// <summary>
    /// Gets a value indicating whether this is a dry run (no actual deletions will occur).
    /// </summary>
    public bool IsDryRun { get; init; } = true;

    /// <summary>
    /// Gets a summary of the retention plan.
    /// </summary>
    /// <returns>A human-readable summary string.</returns>
    public string GetSummary() =>
        $"Retention Plan ({(IsDryRun ? "DRY RUN" : "LIVE")}): " +
        $"Keep {ToKeep.Count} snapshots, Delete {ToDelete.Count} snapshots";
}