namespace Ouroboros.Tests.Pipeline.Branches;

using Ouroboros.Pipeline.Branches;

[Trait("Category", "Unit")]
public class RetentionEvaluatorTests
{
    [Fact]
    public void Evaluate_WithKeepAllPolicy_KeepsEverything()
    {
        var snapshots = CreateSnapshots("branch-a", 5);
        var policy = RetentionPolicy.KeepAll();

        var plan = RetentionEvaluator.Evaluate(snapshots, policy);

        plan.ToKeep.Should().HaveCount(5);
        plan.ToDelete.Should().BeEmpty();
        plan.IsDryRun.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithCountPolicy_KeepsOnlyMaxCount()
    {
        var snapshots = CreateSnapshots("branch-a", 5);
        var policy = RetentionPolicy.ByCount(2);

        var plan = RetentionEvaluator.Evaluate(snapshots, policy);

        plan.ToKeep.Should().HaveCount(2);
        plan.ToDelete.Should().HaveCount(3);
    }

    [Fact]
    public void Evaluate_WithKeepAtLeastOneAndAllExpired_KeepsOne()
    {
        var oldSnapshots = Enumerable.Range(0, 3).Select(i => new SnapshotMetadata
        {
            Id = $"snap-{i}",
            BranchName = "branch-a",
            CreatedAt = DateTime.UtcNow.AddDays(-100),
            Hash = "hash"
        }).ToList();

        var policy = new RetentionPolicy
        {
            MaxAge = TimeSpan.FromDays(1),
            KeepAtLeastOne = true
        };

        var plan = RetentionEvaluator.Evaluate(oldSnapshots, policy);

        plan.ToKeep.Should().HaveCount(1);
        plan.ToDelete.Should().HaveCount(2);
    }

    [Fact]
    public void Evaluate_WithNullSnapshots_ThrowsArgumentNullException()
    {
        var act = () => RetentionEvaluator.Evaluate(null!, RetentionPolicy.KeepAll());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Evaluate_WithNullPolicy_ThrowsArgumentNullException()
    {
        var act = () => RetentionEvaluator.Evaluate([], null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Evaluate_WithDryRunFalse_SetsIsDryRunFalse()
    {
        var snapshots = CreateSnapshots("branch-a", 3);
        var policy = RetentionPolicy.ByCount(2);

        var plan = RetentionEvaluator.Evaluate(snapshots, policy, dryRun: false);

        plan.IsDryRun.Should().BeFalse();
    }

    private static List<SnapshotMetadata> CreateSnapshots(string branch, int count)
    {
        return Enumerable.Range(0, count).Select(i => new SnapshotMetadata
        {
            Id = $"snap-{i}",
            BranchName = branch,
            CreatedAt = DateTime.UtcNow.AddHours(-i),
            Hash = $"hash-{i}"
        }).ToList();
    }
}
