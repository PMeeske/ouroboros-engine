// <copyright file="GoalTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class GoalTests
{
    [Fact]
    public void PrimaryConstructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var description = "Achieve AGI";
        var type = GoalType.Primary;
        var priority = 0.95;
        var subgoals = new List<Goal>();
        var constraints = new Dictionary<string, object> { ["safety"] = true };
        var createdAt = DateTime.UtcNow;

        // Act
        var goal = new Goal(id, description, type, priority, null, subgoals, constraints, createdAt, false, null);

        // Assert
        goal.Id.Should().Be(id);
        goal.Description.Should().Be(description);
        goal.Type.Should().Be(type);
        goal.Priority.Should().Be(priority);
        goal.ParentGoal.Should().BeNull();
        goal.Subgoals.Should().BeSameAs(subgoals);
        goal.Constraints.Should().BeSameAs(constraints);
        goal.CreatedAt.Should().Be(createdAt);
        goal.IsComplete.Should().BeFalse();
        goal.CompletionReason.Should().BeNull();
    }

    [Fact]
    public void ConvenienceConstructor_SetsDefaultValues()
    {
        // Act
        var goal = new Goal("Test goal", GoalType.Secondary, 0.5);

        // Assert
        goal.Id.Should().NotBeEmpty();
        goal.Description.Should().Be("Test goal");
        goal.Type.Should().Be(GoalType.Secondary);
        goal.Priority.Should().Be(0.5);
        goal.ParentGoal.Should().BeNull();
        goal.Subgoals.Should().BeEmpty();
        goal.Constraints.Should().BeEmpty();
        goal.IsComplete.Should().BeFalse();
        goal.CompletionReason.Should().BeNull();
    }

    [Fact]
    public void ConvenienceConstructor_GeneratesUniqueIds()
    {
        var goal1 = new Goal("Goal 1", GoalType.Primary, 1.0);
        var goal2 = new Goal("Goal 2", GoalType.Primary, 1.0);

        goal1.Id.Should().NotBe(goal2.Id);
    }

    [Fact]
    public void ConvenienceConstructor_SetsCreatedAtNearNow()
    {
        var before = DateTime.UtcNow;
        var goal = new Goal("Test", GoalType.Primary, 1.0);
        var after = DateTime.UtcNow;

        goal.CreatedAt.Should().BeOnOrAfter(before);
        goal.CreatedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void With_CanMarkComplete()
    {
        var goal = new Goal("Test", GoalType.Primary, 1.0);

        var completed = goal with { IsComplete = true, CompletionReason = "Achieved" };

        completed.IsComplete.Should().BeTrue();
        completed.CompletionReason.Should().Be("Achieved");
        completed.Description.Should().Be(goal.Description);
    }

    [Fact]
    public void ParentGoal_CanBeSet()
    {
        var parent = new Goal("Parent", GoalType.Primary, 1.0);
        var child = new Goal(
            Guid.NewGuid(), "Child", GoalType.Instrumental, 0.5,
            parent, new List<Goal>(), new Dictionary<string, object>(),
            DateTime.UtcNow, false, null);

        child.ParentGoal.Should().Be(parent);
    }

    [Fact]
    public void Subgoals_CanBePopulated()
    {
        var child1 = new Goal("Sub 1", GoalType.Instrumental, 0.5);
        var child2 = new Goal("Sub 2", GoalType.Instrumental, 0.3);
        var subgoals = new List<Goal> { child1, child2 };

        var parent = new Goal(
            Guid.NewGuid(), "Parent", GoalType.Primary, 1.0,
            null, subgoals, new Dictionary<string, object>(),
            DateTime.UtcNow, false, null);

        parent.Subgoals.Should().HaveCount(2);
        parent.Subgoals.Should().Contain(child1);
        parent.Subgoals.Should().Contain(child2);
    }

    [Theory]
    [InlineData(GoalType.Primary)]
    [InlineData(GoalType.Secondary)]
    [InlineData(GoalType.Instrumental)]
    [InlineData(GoalType.Safety)]
    public void ConvenienceConstructor_AcceptsAllGoalTypes(GoalType type)
    {
        var goal = new Goal("Test", type, 0.5);
        goal.Type.Should().Be(type);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void ConvenienceConstructor_AcceptsVariousPriorities(double priority)
    {
        var goal = new Goal("Test", GoalType.Primary, priority);
        goal.Priority.Should().Be(priority);
    }
}
