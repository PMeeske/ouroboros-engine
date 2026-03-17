using FluentAssertions;
using Ouroboros.Pipeline.Planning;
using Ouroboros.Providers;

namespace Ouroboros.Tests.Planning;

[Trait("Category", "Unit")]
public sealed class SubGoalExtensionsTests
{
    [Fact]
    public void ToSubGoal_WithDefaultIndex_CreatesSubGoalWithIndex0()
    {
        // Arrange
        var goal = Goal.Atomic("Analyze data");

        // Act
        var subGoal = goal.ToSubGoal();

        // Assert
        subGoal.Description.Should().Be("Analyze data");
        subGoal.Id.Should().Be("goal_1"); // index 0 => "goal_1"
    }

    [Fact]
    public void ToSubGoal_WithIndex_CreatesSubGoalWithCorrectId()
    {
        // Arrange
        var goal = Goal.Atomic("Process data");

        // Act
        var subGoal = goal.ToSubGoal(5);

        // Assert
        subGoal.Id.Should().Be("goal_6"); // index 5 => "goal_6"
    }

    [Fact]
    public void ToSubGoal_NullGoal_ThrowsArgumentNullException()
    {
        // Act
        var act = () => SubGoalExtensions.ToSubGoal(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToPipelineGoal_ConvertsSubGoalToGoal()
    {
        // Arrange
        var subGoal = SubGoal.FromDescription("Summarize text", 0);

        // Act
        var goal = subGoal.ToPipelineGoal();

        // Assert
        goal.Description.Should().Be("Summarize text");
        goal.SubGoals.Should().BeEmpty();
    }

    [Fact]
    public void ToPipelineGoal_NullSubGoal_ThrowsArgumentNullException()
    {
        // Act
        var act = () => SubGoalExtensions.ToPipelineGoal(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToSubGoals_ConvertsMultipleGoals()
    {
        // Arrange
        var goals = new[]
        {
            Goal.Atomic("Goal A"),
            Goal.Atomic("Goal B"),
            Goal.Atomic("Goal C")
        };

        // Act
        var subGoals = goals.ToSubGoals();

        // Assert
        subGoals.Should().HaveCount(3);
        subGoals[0].Id.Should().Be("goal_1");
        subGoals[1].Id.Should().Be("goal_2");
        subGoals[2].Id.Should().Be("goal_3");
    }

    [Fact]
    public void ToSubGoals_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var goals = Array.Empty<Goal>();

        // Act
        var subGoals = goals.ToSubGoals();

        // Assert
        subGoals.Should().BeEmpty();
    }

    [Fact]
    public void ToSubGoals_NullEnumerable_ThrowsArgumentNullException()
    {
        // Act
        var act = () => SubGoalExtensions.ToSubGoals(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToPipelineGoals_ConvertsMultipleSubGoals()
    {
        // Arrange
        var subGoals = new[]
        {
            SubGoal.FromDescription("Task 1"),
            SubGoal.FromDescription("Task 2")
        };

        // Act
        var goals = subGoals.ToPipelineGoals();

        // Assert
        goals.Should().HaveCount(2);
        goals[0].Description.Should().Be("Task 1");
        goals[1].Description.Should().Be("Task 2");
    }

    [Fact]
    public void ToPipelineGoals_NullEnumerable_ThrowsArgumentNullException()
    {
        // Act
        var act = () => SubGoalExtensions.ToPipelineGoals(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToHierarchicalGoal_CreatesParentWithChildren()
    {
        // Arrange
        var subGoals = new[]
        {
            SubGoal.FromDescription("Child 1"),
            SubGoal.FromDescription("Child 2"),
            SubGoal.FromDescription("Child 3")
        };

        // Act
        var hierarchical = subGoals.ToHierarchicalGoal("Parent Goal");

        // Assert
        hierarchical.Description.Should().Be("Parent Goal");
        hierarchical.SubGoals.Should().HaveCount(3);
        hierarchical.SubGoals[0].Description.Should().Be("Child 1");
    }

    [Fact]
    public void ToHierarchicalGoal_NullSubGoals_ThrowsArgumentNullException()
    {
        // Act
        var act = () => SubGoalExtensions.ToHierarchicalGoal(null!, "Parent");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToHierarchicalGoal_NullDescription_ThrowsArgumentNullException()
    {
        // Act
        var act = () => Array.Empty<SubGoal>().ToHierarchicalGoal(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetRecommendedTier_ReturnsInferredTier()
    {
        // Arrange
        var goal = Goal.Atomic("find the answer");

        // Act
        var tier = goal.GetRecommendedTier();

        // Assert
        tier.Should().BeOneOf(PathwayTier.Local, PathwayTier.Specialized, PathwayTier.CloudLight, PathwayTier.CloudPremium);
    }

    [Fact]
    public void GetRecommendedTier_NullGoal_ThrowsArgumentNullException()
    {
        // Act
        var act = () => SubGoalExtensions.GetRecommendedTier(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetInferredType_ReturnsSubGoalType()
    {
        // Arrange
        var goal = Goal.Atomic("implement a function");

        // Act
        var type = goal.GetInferredType();

        // Assert
        type.Should().Be(SubGoalType.Coding);
    }

    [Fact]
    public void GetInferredComplexity_ReturnsSubGoalComplexity()
    {
        // Arrange
        var goal = Goal.Atomic("Short task");

        // Act
        var complexity = goal.GetInferredComplexity();

        // Assert
        complexity.Should().Be(SubGoalComplexity.Simple);
    }

    [Fact]
    public void RoundTrip_GoalToSubGoalAndBack_PreservesDescription()
    {
        // Arrange
        var originalGoal = Goal.Atomic("Round trip test");

        // Act
        var subGoal = originalGoal.ToSubGoal();
        var roundTripped = subGoal.ToPipelineGoal();

        // Assert
        roundTripped.Description.Should().Be("Round trip test");
    }
}
