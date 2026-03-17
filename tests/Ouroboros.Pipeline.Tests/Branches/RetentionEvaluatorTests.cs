using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Pipeline.Branches;

[Trait("Category", "Unit")]
public sealed class RetentionEvaluatorTests
{
    private static SnapshotMetadata CreateMetadata(string id, string branch, DateTime createdAt)
    {
        return new SnapshotMetadata
        {
            Id = id,
            BranchName = branch,
            CreatedAt = createdAt,
            Hash = $"hash-{id}"
        };
    }

    #region Null Argument Tests

    [Fact]
    public void Evaluate_WithNullSnapshots_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => RetentionEvaluator.Evaluate(null!, RetentionPolicy.KeepAll());

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Evaluate_WithNullPolicy_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => RetentionEvaluator.Evaluate(
            Array.Empty<SnapshotMetadata>(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region KeepAll Policy Tests

    [Fact]
    public void Evaluate_WithKeepAllPolicy_KeepsAllSnapshots()
    {
        // Arrange
        var snapshots = new[]
        {
            CreateMetadata("s1", "branch-a", DateTime.UtcNow.AddDays(-30)),
            CreateMetadata("s2", "branch-a", DateTime.UtcNow.AddDays(-10)),
            CreateMetadata("s3", "branch-a", DateTime.UtcNow)
        };

        // Act
        var plan = RetentionEvaluator.Evaluate(snapshots, RetentionPolicy.KeepAll());

        // Assert
        plan.ToKeep.Should().HaveCount(3);
        plan.ToDelete.Should().BeEmpty();
    }

    #endregion

    #region Age-Based Policy Tests

    [Fact]
    public void Evaluate_WithAgePolicy_DeletesOldSnapshots()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var snapshots = new[]
        {
            CreateMetadata("old", "branch", now.AddDays(-60)),
            CreateMetadata("recent", "branch", now.AddDays(-2)),
            CreateMetadata("new", "branch", now)
        };
        var policy = RetentionPolicy.ByAge(TimeSpan.FromDays(7));

        // Act
        var plan = RetentionEvaluator.Evaluate(snapshots, policy);

        // Assert
        plan.ToKeep.Should().HaveCount(2);
        plan.ToDelete.Should().HaveCount(1);
        plan.ToDelete[0].Id.Should().Be("old");
    }

    [Fact]
    public void Evaluate_WithAgePolicy_AllExpired_KeepsAtLeastOne()
    {
        // Arrange
        var snapshots = new[]
        {
            CreateMetadata("old1", "branch", DateTime.UtcNow.AddDays(-100)),
            CreateMetadata("old2", "branch", DateTime.UtcNow.AddDays(-90))
        };
        var policy = RetentionPolicy.ByAge(TimeSpan.FromDays(7));

        // Act
        var plan = RetentionEvaluator.Evaluate(snapshots, policy);

        // Assert
        plan.ToKeep.Should().HaveCount(1);
        plan.ToDelete.Should().HaveCount(1);
        // The most recent should be kept
        plan.ToKeep[0].Id.Should().Be("old2");
    }

    [Fact]
    public void Evaluate_WithAgePolicy_KeepAtLeastOneDisabled_DeletesAll()
    {
        // Arrange
        var snapshots = new[]
        {
            CreateMetadata("old1", "branch", DateTime.UtcNow.AddDays(-100)),
            CreateMetadata("old2", "branch", DateTime.UtcNow.AddDays(-90))
        };
        var policy = new RetentionPolicy
        {
            MaxAge = TimeSpan.FromDays(7),
            KeepAtLeastOne = false
        };

        // Act
        var plan = RetentionEvaluator.Evaluate(snapshots, policy);

        // Assert
        plan.ToKeep.Should().BeEmpty();
        plan.ToDelete.Should().HaveCount(2);
    }

    #endregion

    #region Count-Based Policy Tests

    [Fact]
    public void Evaluate_WithCountPolicy_KeepsOnlyMostRecent()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var snapshots = new[]
        {
            CreateMetadata("oldest", "branch", now.AddDays(-30)),
            CreateMetadata("middle", "branch", now.AddDays(-15)),
            CreateMetadata("newest", "branch", now)
        };
        var policy = RetentionPolicy.ByCount(2);

        // Act
        var plan = RetentionEvaluator.Evaluate(snapshots, policy);

        // Assert
        plan.ToKeep.Should().HaveCount(2);
        plan.ToDelete.Should().HaveCount(1);
        plan.ToDelete[0].Id.Should().Be("oldest");
    }

    [Fact]
    public void Evaluate_WithCountPolicy_CountExceedsSnapshots_KeepsAll()
    {
        // Arrange
        var snapshots = new[]
        {
            CreateMetadata("s1", "branch", DateTime.UtcNow),
            CreateMetadata("s2", "branch", DateTime.UtcNow.AddDays(-1))
        };
        var policy = RetentionPolicy.ByCount(10);

        // Act
        var plan = RetentionEvaluator.Evaluate(snapshots, policy);

        // Assert
        plan.ToKeep.Should().HaveCount(2);
        plan.ToDelete.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_WithCountPolicyOfOne_KeepsMostRecent()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var snapshots = new[]
        {
            CreateMetadata("old", "branch", now.AddDays(-30)),
            CreateMetadata("new", "branch", now)
        };
        var policy = RetentionPolicy.ByCount(1);

        // Act
        var plan = RetentionEvaluator.Evaluate(snapshots, policy);

        // Assert
        plan.ToKeep.Should().HaveCount(1);
        plan.ToKeep[0].Id.Should().Be("new");
        plan.ToDelete.Should().HaveCount(1);
    }

    #endregion

    #region Combined Policy Tests

    [Fact]
    public void Evaluate_WithCombinedPolicy_AppliesBothConstraints()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var snapshots = new[]
        {
            CreateMetadata("s1", "branch", now.AddDays(-60)),
            CreateMetadata("s2", "branch", now.AddDays(-3)),
            CreateMetadata("s3", "branch", now.AddDays(-2)),
            CreateMetadata("s4", "branch", now.AddDays(-1)),
            CreateMetadata("s5", "branch", now)
        };
        // Keep max 2, and only if younger than 7 days
        var policy = RetentionPolicy.Combined(TimeSpan.FromDays(7), 2);

        // Act
        var plan = RetentionEvaluator.Evaluate(snapshots, policy);

        // Assert
        // Age filter removes s1, count filter keeps only the 2 most recent from remaining
        plan.ToKeep.Should().HaveCount(2);
        plan.ToDelete.Should().HaveCount(3);
    }

    #endregion

    #region Multi-Branch Tests

    [Fact]
    public void Evaluate_WithMultipleBranches_EvaluatesPerBranch()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var snapshots = new[]
        {
            CreateMetadata("a1", "branch-a", now.AddDays(-30)),
            CreateMetadata("a2", "branch-a", now),
            CreateMetadata("b1", "branch-b", now.AddDays(-30)),
            CreateMetadata("b2", "branch-b", now)
        };
        var policy = RetentionPolicy.ByCount(1);

        // Act
        var plan = RetentionEvaluator.Evaluate(snapshots, policy);

        // Assert
        plan.ToKeep.Should().HaveCount(2); // 1 per branch
        plan.ToDelete.Should().HaveCount(2); // 1 per branch
    }

    #endregion

    #region DryRun Tests

    [Fact]
    public void Evaluate_DefaultDryRunTrue_SetsDryRunInPlan()
    {
        // Act
        var plan = RetentionEvaluator.Evaluate(Array.Empty<SnapshotMetadata>(), RetentionPolicy.KeepAll());

        // Assert
        plan.IsDryRun.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithDryRunFalse_SetsDryRunInPlan()
    {
        // Act
        var plan = RetentionEvaluator.Evaluate(Array.Empty<SnapshotMetadata>(), RetentionPolicy.KeepAll(), dryRun: false);

        // Assert
        plan.IsDryRun.Should().BeFalse();
    }

    #endregion

    #region Empty Input Tests

    [Fact]
    public void Evaluate_WithEmptySnapshots_ReturnsEmptyPlan()
    {
        // Act
        var plan = RetentionEvaluator.Evaluate(Array.Empty<SnapshotMetadata>(), RetentionPolicy.ByCount(5));

        // Assert
        plan.ToKeep.Should().BeEmpty();
        plan.ToDelete.Should().BeEmpty();
    }

    #endregion
}
