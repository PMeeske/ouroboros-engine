// <copyright file="AgentModelTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.TheoryOfMind;

namespace Ouroboros.Tests.TheoryOfMind;

[Trait("Category", "Unit")]
public sealed class AgentModelTests
{
    [Fact]
    public void Create_InitializesWithEmptyState()
    {
        // Act
        var model = AgentModel.Create("agent-42");

        // Assert
        model.AgentId.Should().Be("agent-42");
        model.Beliefs.AgentId.Should().Be("agent-42");
        model.Beliefs.Beliefs.Should().BeEmpty();
        model.InferredGoals.Should().BeEmpty();
        model.InferredCapabilities.Should().BeEmpty();
        model.Personality.Cooperativeness.Should().Be(0.5);
        model.ObservationHistory.Should().BeEmpty();
        model.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        model.LastInteraction.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void WithObservation_AddsToHistory()
    {
        // Arrange
        var model = AgentModel.Create("agent-1");
        var observation = AgentObservation.Action("agent-1", "performed task");

        // Act
        var updated = model.WithObservation(observation);

        // Assert
        updated.ObservationHistory.Should().HaveCount(1);
        updated.ObservationHistory[0].Content.Should().Be("performed task");
        updated.LastInteraction.Should().Be(observation.ObservedAt);
    }

    [Fact]
    public void WithObservation_DoesNotMutateOriginal()
    {
        // Arrange
        var model = AgentModel.Create("agent-1");
        var observation = AgentObservation.Action("agent-1", "did something");

        // Act
        _ = model.WithObservation(observation);

        // Assert
        model.ObservationHistory.Should().BeEmpty();
    }

    [Fact]
    public void WithBeliefs_ReplacesBeliefState()
    {
        // Arrange
        var model = AgentModel.Create("agent-1");
        var newBeliefs = BeliefState.Empty("agent-1").WithConfidence(0.9);

        // Act
        var updated = model.WithBeliefs(newBeliefs);

        // Assert
        updated.Beliefs.Confidence.Should().Be(0.9);
    }

    [Fact]
    public void WithGoal_AddsNewGoal()
    {
        // Arrange
        var model = AgentModel.Create("agent-1");

        // Act
        var updated = model.WithGoal("complete task");

        // Assert
        updated.InferredGoals.Should().Contain("complete task");
    }

    [Fact]
    public void WithGoal_DoesNotDuplicate()
    {
        // Arrange
        var model = AgentModel.Create("agent-1").WithGoal("complete task");

        // Act
        var updated = model.WithGoal("complete task");

        // Assert
        updated.InferredGoals.Should().HaveCount(1);
        updated.Should().BeSameAs(model);
    }

    [Fact]
    public void WithCapability_AddsNewCapability()
    {
        // Arrange
        var model = AgentModel.Create("agent-1");

        // Act
        var updated = model.WithCapability("code generation");

        // Assert
        updated.InferredCapabilities.Should().Contain("code generation");
    }

    [Fact]
    public void WithCapability_DoesNotDuplicate()
    {
        // Arrange
        var model = AgentModel.Create("agent-1").WithCapability("reasoning");

        // Act
        var updated = model.WithCapability("reasoning");

        // Assert
        updated.InferredCapabilities.Should().HaveCount(1);
        updated.Should().BeSameAs(model);
    }

    [Fact]
    public void WithPersonality_ReplacesTraits()
    {
        // Arrange
        var model = AgentModel.Create("agent-1");
        var traits = new PersonalityTraits(0.9, 0.8, 0.7, new Dictionary<string, double>());

        // Act
        var updated = model.WithPersonality(traits);

        // Assert
        updated.Personality.Cooperativeness.Should().Be(0.9);
        updated.Personality.Predictability.Should().Be(0.8);
        updated.Personality.Competence.Should().Be(0.7);
    }

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var beliefs = BeliefState.Empty("a");
        var goals = new List<string> { "g1" };
        var caps = new List<string> { "c1" };
        var personality = PersonalityTraits.Default();
        var history = new List<AgentObservation>();
        var created = DateTime.UtcNow;
        var interaction = DateTime.UtcNow;

        // Act
        var model = new AgentModel("a", beliefs, goals, caps, personality, history, created, interaction);

        // Assert
        model.AgentId.Should().Be("a");
        model.InferredGoals.Should().HaveCount(1);
        model.InferredCapabilities.Should().HaveCount(1);
    }
}
