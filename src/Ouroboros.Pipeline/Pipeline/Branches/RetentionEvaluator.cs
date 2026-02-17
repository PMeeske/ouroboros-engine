namespace Ouroboros.Pipeline.Branches;

/// <summary>
/// Evaluates retention policies and generates retention plans.
/// </summary>
public static class RetentionEvaluator
{
    /// <summary>
    /// Evaluates a retention policy against a collection of snapshots.
    /// </summary>
    /// <param name="snapshots">The snapshots to evaluate.</param>
    /// <param name="policy">The retention policy to apply.</param>
    /// <param name="dryRun">Whether this is a dry run (default: true).</param>
    /// <returns>A retention plan indicating which snapshots to keep and delete.</returns>
    public static RetentionPlan Evaluate(
        IEnumerable<SnapshotMetadata> snapshots,
        RetentionPolicy policy,
        bool dryRun = true)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(policy);

        var snapshotList = snapshots.ToList();
        var now = DateTime.UtcNow;

        // Group by branch for per-branch evaluation
        var byBranch = snapshotList.GroupBy(s => s.BranchName);
        var toKeep = new List<SnapshotMetadata>();
        var toDelete = new List<SnapshotMetadata>();

        foreach (var branchGroup in byBranch)
        {
            var branchSnapshots = branchGroup.OrderByDescending(s => s.CreatedAt).ToList();

            // Apply age filter if specified
            var afterAgeFilter = policy.MaxAge.HasValue
                ? branchSnapshots.Where(s => now - s.CreatedAt <= policy.MaxAge.Value).ToList()
                : branchSnapshots;

            // Apply count filter if specified
            var afterCountFilter = policy.MaxCount.HasValue
                ? afterAgeFilter.Take(policy.MaxCount.Value).ToList()
                : afterAgeFilter;

            // Ensure at least one snapshot is kept if required
            if (policy.KeepAtLeastOne && afterCountFilter.Count == 0 && branchSnapshots.Count > 0)
            {
                afterCountFilter = [branchSnapshots.First()];
            }

            toKeep.AddRange(afterCountFilter);
            toDelete.AddRange(branchSnapshots.Except(afterCountFilter));
        }

        return new RetentionPlan
        {
            ToKeep = toKeep.AsReadOnly(),
            ToDelete = toDelete.AsReadOnly(),
            IsDryRun = dryRun
        };
    }
}