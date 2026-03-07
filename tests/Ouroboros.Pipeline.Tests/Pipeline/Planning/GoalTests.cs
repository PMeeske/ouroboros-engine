namespace Ouroboros.Tests.Pipeline.Planning;

using Ouroboros.Pipeline.Planning;

[Trait("Category", "Unit")]
public class GoalTests
{
    [Fact]
    public void Atomic_CreatesGoalWithNoSubGoals()
    {
        var goal = Goal.Atomic("test goal");

        goal.Description.Should().Be("test goal");
        goal.SubGoals.Should().BeEmpty();
        goal.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Atomic_ThrowsOnNull()
    {
        var act = () => Goal.Atomic(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithSubGoals_ReturnsNewGoalWithSubGoals()
    {
        var parent = Goal.Atomic("parent");
        var child1 = Goal.Atomic("child1");
        var child2 = Goal.Atomic("child2");

        var updated = parent.WithSubGoals(child1, child2);

        updated.SubGoals.Should().HaveCount(2);
        parent.SubGoals.Should().BeEmpty();
    }

    [Fact]
    public void AppendSubGoals_AddsToExisting()
    {
        var parent = Goal.Atomic("parent")
            .WithSubGoals(Goal.Atomic("child1"));

        var updated = parent.AppendSubGoals(Goal.Atomic("child2"));

        updated.SubGoals.Should().HaveCount(2);
    }

    [Fact]
    public void ToOption_ReturnsNoneForEmptyDescription()
    {
        var goal = Goal.Atomic("test") with { Description = "" };
        goal.ToOption().HasValue.Should().BeFalse();
    }

    [Fact]
    public void ToOption_ReturnsSomeForValidGoal()
    {
        var goal = Goal.Atomic("test");
        goal.ToOption().HasValue.Should().BeTrue();
    }
}
