namespace Ouroboros.Tests.Pipeline.Branches;

using Ouroboros.Pipeline.Branches;

[Trait("Category", "Unit")]
public class RetentionPlanTests
{
    [Fact]
    public void GetSummary_DryRun_ContainsDryRunLabel()
    {
        var plan = new RetentionPlan
        {
            ToKeep = new List<SnapshotMetadata>(),
            ToDelete = new List<SnapshotMetadata>(),
            IsDryRun = true
        };

        plan.GetSummary().Should().Contain("DRY RUN");
    }

    [Fact]
    public void GetSummary_Live_ContainsLiveLabel()
    {
        var plan = new RetentionPlan
        {
            ToKeep = new List<SnapshotMetadata>(),
            ToDelete = new List<SnapshotMetadata>(),
            IsDryRun = false
        };

        plan.GetSummary().Should().Contain("LIVE");
    }

    [Fact]
    public void GetSummary_IncludesCorrectCounts()
    {
        var plan = new RetentionPlan
        {
            ToKeep = CreateSnapshots(3),
            ToDelete = CreateSnapshots(2),
            IsDryRun = true
        };

        plan.GetSummary().Should().Contain("Keep 3").And.Contain("Delete 2");
    }

    private static List<SnapshotMetadata> CreateSnapshots(int count)
    {
        return Enumerable.Range(0, count).Select(i => new SnapshotMetadata
        {
            Id = $"snap-{i}",
            BranchName = "b",
            CreatedAt = DateTime.UtcNow,
            Hash = "h"
        }).ToList();
    }
}
