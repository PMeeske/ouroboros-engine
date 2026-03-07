namespace Ouroboros.Tests.Pipeline.Planning;

using Ouroboros.Pipeline.Planning;

[Trait("Category", "Unit")]
public class GoalExtensionsTests
{
    [Fact]
    public void Flatten_ReturnsAllGoalsInHierarchy()
    {
        var child1 = Goal.Atomic("c1");
        var child2 = Goal.Atomic("c2");
        var parent = Goal.Atomic("parent").WithSubGoals(child1, child2);

        var flattened = parent.Flatten().ToList();

        flattened.Should().HaveCount(3);
    }

    [Fact]
    public void TotalCount_CountsAllGoals()
    {
        var parent = Goal.Atomic("parent")
            .WithSubGoals(
                Goal.Atomic("c1"),
                Goal.Atomic("c2").WithSubGoals(Goal.Atomic("gc1")));

        parent.TotalCount().Should().Be(4);
    }

    [Fact]
    public void GetLeafGoals_ReturnsOnlyLeaves()
    {
        var leaf1 = Goal.Atomic("leaf1");
        var leaf2 = Goal.Atomic("leaf2");
        var parent = Goal.Atomic("parent").WithSubGoals(leaf1, leaf2);

        var leaves = parent.GetLeafGoals().ToList();
        leaves.Should().HaveCount(2);
    }

    [Fact]
    public void MaxDepth_ReturnsCorrectDepth()
    {
        var deep = Goal.Atomic("root")
            .WithSubGoals(
                Goal.Atomic("level1")
                    .WithSubGoals(Goal.Atomic("level2")));

        deep.MaxDepth().Should().Be(3);
    }

    [Fact]
    public void MaxDepth_IsOneForAtomicGoal()
    {
        Goal.Atomic("atomic").MaxDepth().Should().Be(1);
    }

    [Fact]
    public void ToTreeString_ContainsDescription()
    {
        var goal = Goal.Atomic("root")
            .WithSubGoals(Goal.Atomic("child"));

        var tree = goal.ToTreeString();

        tree.Should().Contain("root");
        tree.Should().Contain("child");
    }

    [Fact]
    public void Filter_RemovesNonMatchingGoals()
    {
        var goal = Goal.Atomic("include");
        var result = goal.Filter(g => g.Description.Contains("include"));

        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void Filter_ReturnsNoneWhenRootDoesNotMatch()
    {
        var goal = Goal.Atomic("exclude");
        var result = goal.Filter(g => g.Description.Contains("include"));

        result.HasValue.Should().BeFalse();
    }
}
