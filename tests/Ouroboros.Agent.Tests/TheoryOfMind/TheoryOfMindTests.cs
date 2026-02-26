// <copyright file="TheoryOfMindTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.TheoryOfMind;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests.TheoryOfMind;

[Trait("Category", "Unit")]
public class TheoryOfMindTests
{
    private readonly Mock<IChatCompletionModel> _llmMock = new();

    [Fact]
    public void Constructor_NullLlm_Throws()
    {
        var act = () => new Ouroboros.Agent.TheoryOfMind.TheoryOfMind(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task InferBeliefsAsync_EmptyAgentId_ReturnsFailure()
    {
        var tom = CreateToM();
        var result = await tom.InferBeliefsAsync("", new List<AgentObservation>());
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task InferBeliefsAsync_NullObservations_ReturnsEmptyState()
    {
        var tom = CreateToM();
        var result = await tom.InferBeliefsAsync("agent1", null);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task InferBeliefsAsync_EmptyObservations_ReturnsEmptyState()
    {
        var tom = CreateToM();
        var result = await tom.InferBeliefsAsync("agent1", new List<AgentObservation>());

        result.IsSuccess.Should().BeTrue();
        result.Value.AgentId.Should().Be("agent1");
    }

    [Fact]
    public async Task PredictIntentionAsync_EmptyAgentId_ReturnsFailure()
    {
        var tom = CreateToM();
        var beliefs = BeliefState.Empty("test");

        var result = await tom.PredictIntentionAsync("", beliefs);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task PredictIntentionAsync_NullBeliefs_ReturnsUnknown()
    {
        var tom = CreateToM();
        var result = await tom.PredictIntentionAsync("agent1", null!);

        result.IsSuccess.Should().BeTrue();
        result.Value.PredictedGoal.Should().Contain("Unknown");
    }

    [Fact]
    public async Task PredictNextActionAsync_EmptyAgentId_ReturnsFailure()
    {
        var tom = CreateToM();
        var beliefs = BeliefState.Empty("test");

        var result = await tom.PredictNextActionAsync("", beliefs, new List<Ouroboros.Domain.Embodied.EmbodiedAction>());
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAgentModelAsync_EmptyAgentId_ReturnsFailure()
    {
        var tom = CreateToM();
        var obs = new AgentObservation("agent1", "action", "did something",
            new Dictionary<string, object>(), DateTime.UtcNow);

        var result = await tom.UpdateAgentModelAsync("", obs);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAgentModelAsync_NullObservation_ReturnsFailure()
    {
        var tom = CreateToM();
        var result = await tom.UpdateAgentModelAsync("agent1", null!);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GetAgentModel_EmptyId_ReturnsNull()
    {
        var tom = CreateToM();
        tom.GetAgentModel("").Should().BeNull();
    }

    [Fact]
    public void GetAgentModel_UnknownId_ReturnsNull()
    {
        var tom = CreateToM();
        tom.GetAgentModel("unknown-agent").Should().BeNull();
    }

    [Fact]
    public async Task GetModelConfidenceAsync_UnknownAgent_ReturnsZero()
    {
        var tom = CreateToM();
        var confidence = await tom.GetModelConfidenceAsync("unknown");
        confidence.Should().Be(0.0);
    }

    private Ouroboros.Agent.TheoryOfMind.TheoryOfMind CreateToM()
    {
        return new Ouroboros.Agent.TheoryOfMind.TheoryOfMind(_llmMock.Object);
    }
}
