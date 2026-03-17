using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Pipeline.Branches;

[Trait("Category", "Unit")]
public sealed class RetentionPlanTests
{
    private static SnapshotMetadata CreateMetadata(string id = "snap-1", string branch = "main")
    {
        return new SnapshotMetadata
        {
            Id = id,
            BranchName = branch,
            CreatedAt = DateTime.UtcNow,
            Hash = "abc123"
        };
    }

    [Fact]
    public void Constructor_WithRequiredProperties_SetsValues()
    {
        // Arrange
        var toKeep = new List<SnapshotMetadata> { CreateMetadata("keep-1") };
        var toDelete = new List<SnapshotMetadata> { CreateMetadata("delete-1") };

        // Act
        var plan = new RetentionPlan
        {
            ToKeep = toKeep,
            ToDelete = toDelete
        };

        // Assert
        plan.ToKeep.Should().HaveCount(1);
        plan.ToDelete.Should().HaveCount(1);
    }

    [Fact]
    public void IsDryRun_DefaultsToTrue()
    {
        // Act
        var plan = new RetentionPlan
        {
            ToKeep = new List<SnapshotMetadata>(),
            ToDelete = new List<SnapshotMetadata>()
        };

        // Assert
        plan.IsDryRun.Should().BeTrue();
    }

    [Fact]
    public void IsDryRun_CanBeSetToFalse()
    {
        // Act
        var plan = new RetentionPlan
        {
            ToKeep = new List<SnapshotMetadata>(),
            ToDelete = new List<SnapshotMetadata>(),
            IsDryRun = false
        };

        // Assert
        plan.IsDryRun.Should().BeFalse();
    }

    [Fact]
    public void GetSummary_WithDryRun_ContainsDryRunLabel()
    {
        // Arrange
        var plan = new RetentionPlan
        {
            ToKeep = new List<SnapshotMetadata> { CreateMetadata("k1"), CreateMetadata("k2") },
            ToDelete = new List<SnapshotMetadata> { CreateMetadata("d1") },
            IsDryRun = true
        };

        // Act
        string summary = plan.GetSummary();

        // Assert
        summary.Should().Contain("DRY RUN");
        summary.Should().Contain("Keep 2");
        summary.Should().Contain("Delete 1");
    }

    [Fact]
    public void GetSummary_WithLiveRun_ContainsLiveLabel()
    {
        // Arrange
        var plan = new RetentionPlan
        {
            ToKeep = new List<SnapshotMetadata> { CreateMetadata() },
            ToDelete = new List<SnapshotMetadata>(),
            IsDryRun = false
        };

        // Act
        string summary = plan.GetSummary();

        // Assert
        summary.Should().Contain("LIVE");
        summary.Should().Contain("Keep 1");
        summary.Should().Contain("Delete 0");
    }

    [Fact]
    public void GetSummary_WithEmptyLists_ShowsZeroCounts()
    {
        // Arrange
        var plan = new RetentionPlan
        {
            ToKeep = new List<SnapshotMetadata>(),
            ToDelete = new List<SnapshotMetadata>()
        };

        // Act
        string summary = plan.GetSummary();

        // Assert
        summary.Should().Contain("Keep 0");
        summary.Should().Contain("Delete 0");
    }
}
