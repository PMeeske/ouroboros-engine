// <copyright file="ImprovementPlanTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class ImprovementPlanTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var goal = "Improve response accuracy";
        var actions = new List<string> { "Fine-tune model", "Expand training data" };
        var improvements = new Dictionary<string, double> { ["accuracy"] = 0.15, ["latency"] = -0.05 };
        var duration = TimeSpan.FromHours(4);
        var priority = 0.8;
        var createdAt = DateTime.UtcNow;

        // Act
        var plan = new ImprovementPlan(goal, actions, improvements, duration, priority, createdAt);

        // Assert
        plan.Goal.Should().Be(goal);
        plan.Actions.Should().BeEquivalentTo(actions);
        plan.ExpectedImprovements.Should().BeEquivalentTo(improvements);
        plan.EstimatedDuration.Should().Be(duration);
        plan.Priority.Should().Be(priority);
        plan.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Constructor_WithEmptyActions_Succeeds()
    {
        var plan = new ImprovementPlan(
            "goal", new List<string>(), new Dictionary<string, double>(),
            TimeSpan.Zero, 0.5, DateTime.UtcNow);

        plan.Actions.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithEmptyImprovements_Succeeds()
    {
        var plan = new ImprovementPlan(
            "goal", new List<string> { "action" }, new Dictionary<string, double>(),
            TimeSpan.FromMinutes(30), 0.5, DateTime.UtcNow);

        plan.ExpectedImprovements.Should().BeEmpty();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var actions = new List<string> { "a" };
        var improvements = new Dictionary<string, double> { ["x"] = 0.1 };
        var time = DateTime.UtcNow;
        var duration = TimeSpan.FromHours(1);

        var a = new ImprovementPlan("goal", actions, improvements, duration, 0.5, time);
        var b = new ImprovementPlan("goal", actions, improvements, duration, 0.5, time);

        a.Should().Be(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = new ImprovementPlan(
            "goal", new List<string>(), new Dictionary<string, double>(),
            TimeSpan.FromHours(1), 0.5, DateTime.UtcNow);

        var modified = original with { Priority = 1.0 };

        modified.Priority.Should().Be(1.0);
        modified.Goal.Should().Be(original.Goal);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Constructor_AcceptsVariousPriorities(double priority)
    {
        var plan = new ImprovementPlan(
            "goal", new List<string>(), new Dictionary<string, double>(),
            TimeSpan.Zero, priority, DateTime.UtcNow);

        plan.Priority.Should().Be(priority);
    }
}
