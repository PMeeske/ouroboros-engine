// <copyright file="WorldModelPlanTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.WorldModel;
using Action = Ouroboros.Agent.MetaAI.WorldModel.Action;
using WorldModelPlan = Ouroboros.Agent.MetaAI.WorldModel.Plan;

namespace Ouroboros.Agent.Tests.MetaAI.WorldModel;

/// <summary>
/// Unit tests for the WorldModel Plan record.
/// </summary>
[Trait("Category", "Unit")]
public class WorldModelPlanTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsAllProperties()
    {
        // Arrange
        var actions = new List<Action>
        {
            new("move", new Dictionary<string, object> { ["target"] = "A" }),
            new("analyze", new Dictionary<string, object>())
        };

        // Act
        var sut = new WorldModelPlan("Navigate to target", actions, 5.0, 0.85);

        // Assert
        sut.Description.Should().Be("Navigate to target");
        sut.Actions.Should().HaveCount(2);
        sut.ExpectedReward.Should().Be(5.0);
        sut.Confidence.Should().Be(0.85);
    }

    [Fact]
    public void Constructor_EmptyActions_Succeeds()
    {
        // Arrange & Act
        var sut = new WorldModelPlan("empty plan", new List<Action>(), 0.0, 0.0);

        // Assert
        sut.Actions.Should().BeEmpty();
    }

    [Fact]
    public void Equality_TwoIdenticalPlans_AreEqual()
    {
        // Arrange
        var actions = new List<Action> { new("act", new Dictionary<string, object>()) };
        var a = new WorldModelPlan("plan", actions, 1.0, 0.5);
        var b = new WorldModelPlan("plan", actions, 1.0, 0.5);

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void With_ModifiedConfidence_CreatesNewRecord()
    {
        // Arrange
        var original = new WorldModelPlan(
            "plan",
            new List<Action>(),
            1.0,
            0.5);

        // Act
        var modified = original with { Confidence = 0.9 };

        // Assert
        modified.Confidence.Should().Be(0.9);
        original.Confidence.Should().Be(0.5);
    }

    [Fact]
    public void Constructor_NegativeReward_SetsCorrectly()
    {
        // Arrange & Act
        var sut = new WorldModelPlan("risky plan", new List<Action>(), -3.0, 0.2);

        // Assert
        sut.ExpectedReward.Should().Be(-3.0);
    }
}
