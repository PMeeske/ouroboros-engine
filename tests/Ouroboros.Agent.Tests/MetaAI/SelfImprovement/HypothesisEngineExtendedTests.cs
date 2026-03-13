// <copyright file="HypothesisEngineExtendedTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;
using MetaAIPlanStep = Ouroboros.Agent.PlanStep;
using MetaAIHypothesis = Ouroboros.Agent.MetaAI.Hypothesis;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class HypothesisEngineExtendedTests
{
    private readonly Mock<IChatCompletionModel> _llmMock = new();
    private readonly Mock<IMetaAIPlannerOrchestrator> _orchestratorMock = new();
    private readonly Mock<IMemoryStore> _memoryMock = new();
    private readonly Mock<Ouroboros.Core.Ethics.IEthicsFramework> _ethicsMock = new();

    [Fact]
    public void Constructor_NullLlm_Throws()
    {
        var act = () => new HypothesisEngine(null!, _orchestratorMock.Object, _memoryMock.Object, _ethicsMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullOrchestrator_Throws()
    {
        var act = () => new HypothesisEngine(_llmMock.Object, null!, _memoryMock.Object, _ethicsMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GenerateHypothesisAsync_EmptyObservation_ReturnsFailure()
    {
        var engine = CreateEngine();
        var result = await engine.GenerateHypothesisAsync("  ");
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DesignExperimentAsync_NullHypothesis_ReturnsFailure()
    {
        var engine = CreateEngine();
        var result = await engine.DesignExperimentAsync(null!);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task TestHypothesisAsync_NullHypothesis_ReturnsFailure()
    {
        var engine = CreateEngine();
        var experiment = new Experiment(Guid.NewGuid(), null!, "test", new List<MetaAIPlanStep>(),
            new Dictionary<string, object>(), DateTime.UtcNow);
        var result = await engine.TestHypothesisAsync(null!, experiment);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task TestHypothesisAsync_NullExperiment_ReturnsFailure()
    {
        var engine = CreateEngine();
        var hypothesis = new MetaAIHypothesis(Guid.NewGuid(), "stmt", "domain", 0.5,
            new List<string>(), new List<string>(), DateTime.UtcNow, false, null);
        var result = await engine.TestHypothesisAsync(hypothesis, null!);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task AbductiveReasoningAsync_NullObservations_ReturnsFailure()
    {
        var engine = CreateEngine();
        var result = await engine.AbductiveReasoningAsync(null!);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task AbductiveReasoningAsync_EmptyObservations_ReturnsFailure()
    {
        var engine = CreateEngine();
        var result = await engine.AbductiveReasoningAsync(new List<string>());
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GetHypothesesByDomain_EmptyDomain_ReturnsEmpty()
    {
        var engine = CreateEngine();
        engine.GetHypothesesByDomain("  ").Should().BeEmpty();
    }

    [Fact]
    public void GetConfidenceTrend_UnknownId_ReturnsEmpty()
    {
        var engine = CreateEngine();
        engine.GetConfidenceTrend(Guid.NewGuid()).Should().BeEmpty();
    }

    [Fact]
    public void UpdateHypothesis_UnknownId_DoesNotThrow()
    {
        var engine = CreateEngine();
        engine.UpdateHypothesis(Guid.NewGuid(), "evidence", true);
    }

    private HypothesisEngine CreateEngine()
    {
        return new HypothesisEngine(_llmMock.Object, _orchestratorMock.Object, _memoryMock.Object, _ethicsMock.Object);
    }
}
