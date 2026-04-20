#pragma warning disable CA2000 // Test file - ownership is managed by test lifecycle
using FluentAssertions;
using Ouroboros.Providers;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class CollectiveMindTests : IDisposable
{
    private readonly CollectiveMind _sut;

    public CollectiveMindTests()
    {
        _sut = new CollectiveMind();
    }

    [Fact]
    public void Ctor_DefaultElectionStrategy_IsWeightedMajority()
    {
        _sut.ElectionStrategy.Should().Be(ElectionStrategy.WeightedMajority);
    }

    [Fact]
    public void Ctor_CustomElectionStrategy_IsPreserved()
    {
        using var mind = new CollectiveMind(ElectionStrategy.BordaCount);
        mind.ElectionStrategy.Should().Be(ElectionStrategy.BordaCount);
    }

    [Fact]
    public void ThinkingMode_DefaultsToAdaptive()
    {
        _sut.ThinkingMode.Should().Be(CollectiveThinkingMode.Adaptive);
    }

    [Fact]
    public void ThinkingMode_CanBeSet()
    {
        _sut.ThinkingMode = CollectiveThinkingMode.Racing;
        _sut.ThinkingMode.Should().Be(CollectiveThinkingMode.Racing);
    }

    [Fact]
    public void Pathways_InitiallyEmpty()
    {
        _sut.Pathways.Should().BeEmpty();
    }

    [Fact]
    public void HealthyPathwayCount_InitiallyZero()
    {
        _sut.HealthyPathwayCount.Should().Be(0);
    }

    [Fact]
    public void CostTracker_IsNotNull()
    {
        _sut.CostTracker.Should().NotBeNull();
    }

    [Fact]
    public void ThoughtStream_IsObservable()
    {
        _sut.ThoughtStream.Should().NotBeNull();
    }

    [Fact]
    public void SubGoalStream_IsObservable()
    {
        _sut.SubGoalStream.Should().NotBeNull();
    }

    [Fact]
    public void ElectionEvents_IsObservable()
    {
        _sut.ElectionEvents.Should().NotBeNull();
    }

    [Fact]
    public void GetOptimizationSuggestions_WithNoHistory_ReturnsEmpty()
    {
        _sut.GetOptimizationSuggestions().Should().BeEmpty();
    }

    [Fact]
    public void DecompositionConfig_DefaultsToDefault()
    {
        _sut.DecompositionConfig.Should().NotBeNull();
    }

    [Fact]
    public void DecompositionConfig_SetToNull_UsesDefault()
    {
        _sut.DecompositionConfig = null!;
        _sut.DecompositionConfig.Should().NotBeNull();
    }

    [Fact]
    public void DecompositionConfig_CanBeSet()
    {
        var config = DecompositionConfig.LocalFirst;
        _sut.DecompositionConfig = config;
        _sut.DecompositionConfig.Should().BeSameAs(config);
    }

    [Fact]
    public void ElectionStrategy_CanBeChanged()
    {
        _sut.ElectionStrategy = ElectionStrategy.InstantRunoff;
        _sut.ElectionStrategy.Should().Be(ElectionStrategy.InstantRunoff);
    }

    [Fact]
    public async Task GenerateTextAsync_WithNoPathways_Throws()
    {
        await FluentActions.Invoking(() => _sut.GenerateTextAsync("test"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GenerateWithThinkingAsync_WithNoPathways_Throws()
    {
        await FluentActions.Invoking(() => _sut.GenerateWithThinkingAsync("test"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void SetMaster_WithNonexistentPathway_DoesNotThrow()
    {
        FluentActions.Invoking(() => _sut.SetMaster("nonexistent"))
            .Should().NotThrow();
    }

    [Fact]
    public void SetFirstAsMaster_WithNoPathways_DoesNotThrow()
    {
        FluentActions.Invoking(() => _sut.SetFirstAsMaster())
            .Should().NotThrow();
    }

    [Fact]
    public void ComputePhi_WithNoPathways_ReturnsResult()
    {
        var result = _sut.ComputePhi();
        result.Should().NotBeNull();
    }

    [Fact]
    public void GetConsciousnessStatus_ReturnsFormattedString()
    {
        var status = _sut.GetConsciousnessStatus();
        status.Should().Contain("Collective Mind Status");
        status.Should().Contain("Mode:");
        status.Should().Contain("Pathways:");
    }

    [Fact]
    public void StreamWithThinkingAsync_ReturnsObservable()
    {
        var observable = _sut.StreamWithThinkingAsync("test");
        observable.Should().NotBeNull();
    }

    [Fact]
    public void StreamReasoningContent_ReturnsObservable()
    {
        var observable = _sut.StreamReasoningContent("test");
        observable.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        FluentActions.Invoking(() => _sut.Dispose()).Should().NotThrow();
    }

    [Fact]
    public void Dispose_TwiceDoesNotThrow()
    {
        _sut.Dispose();
        FluentActions.Invoking(() => _sut.Dispose()).Should().NotThrow();
    }

    [Fact]
    public void ConfigurePathway_WithNonexistentPathway_DoesNotThrow()
    {
        FluentActions.Invoking(() => _sut.ConfigurePathway("missing", PathwayTier.CloudPremium, SubGoalType.Coding))
            .Should().NotThrow();
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}
