// <copyright file="CuriosityEngineTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;
using MetaAIPlan = Ouroboros.Agent.MetaAI.Plan;
using MetaAIPlanStep = Ouroboros.Agent.PlanStep;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class CuriosityEngineTests
{
    private readonly Mock<IChatCompletionModel> _llmMock = new();
    private readonly Mock<IMemoryStore> _memoryMock = new();
    private readonly Mock<ISkillRegistry> _skillsMock = new();
    private readonly Mock<ISafetyGuard> _safetyMock = new();
    private readonly Mock<Ouroboros.Core.Ethics.IEthicsFramework> _ethicsMock = new();

    [Fact]
    public void Constructor_NullLlm_Throws()
    {
        var act = () => new CuriosityEngine(null!, _memoryMock.Object, _skillsMock.Object,
            _safetyMock.Object, _ethicsMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullMemory_Throws()
    {
        var act = () => new CuriosityEngine(_llmMock.Object, null!, _skillsMock.Object,
            _safetyMock.Object, _ethicsMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ComputeNoveltyAsync_NullPlan_ReturnsZero()
    {
        var engine = CreateEngine();
        var novelty = await engine.ComputeNoveltyAsync(null!);
        novelty.Should().Be(0.0);
    }

    [Fact]
    public async Task ComputeNoveltyAsync_NoSimilarExperiences_ReturnsOne()
    {
        _memoryMock.Setup(m => m.QueryExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Experience>, string>.Success(new List<Experience>()));

        var engine = CreateEngine();
        var plan = new MetaAIPlan("New goal", new List<MetaAIPlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow);

        var novelty = await engine.ComputeNoveltyAsync(plan);

        novelty.Should().Be(1.0);
    }

    [Fact]
    public async Task EstimateInformationGainAsync_EmptyDescription_ReturnsZero()
    {
        var engine = CreateEngine();
        var gain = await engine.EstimateInformationGainAsync("  ");
        gain.Should().Be(0.0);
    }

    [Fact]
    public void RecordExploration_NullPlan_DoesNotThrow()
    {
        var engine = CreateEngine();
        engine.RecordExploration(null!, null!, 0.5);
    }

    [Fact]
    public void GetExplorationStats_ReturnsStats()
    {
        var engine = CreateEngine();

        var stats = engine.GetExplorationStats();

        stats.Should().ContainKey("total_explorations");
        stats.Should().ContainKey("session_explorations");
    }

    private CuriosityEngine CreateEngine(CuriosityEngineConfig? config = null)
    {
        return new CuriosityEngine(_llmMock.Object, _memoryMock.Object, _skillsMock.Object,
            _safetyMock.Object, _ethicsMock.Object, config);
    }
}
