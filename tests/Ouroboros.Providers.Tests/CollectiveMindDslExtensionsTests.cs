#pragma warning disable CA2000 // Test file - ownership is managed by test lifecycle
using FluentAssertions;
using Ouroboros.Providers;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class CollectiveMindDslExtensionsTests : IDisposable
{
    private readonly CollectiveMind _mind;

    public CollectiveMindDslExtensionsTests()
    {
        _mind = CollectiveMindFactory.CreateLocal();
    }

    [Fact]
    public void Stream_ReturnsStreamingPipeline()
    {
        var pipeline = _mind.Stream();
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void WithElection_SetStrategy_ReturnsSameMind()
    {
        var result = _mind.WithElection(ElectionStrategy.BordaCount);
        result.Should().BeSameAs(_mind);
        _mind.ElectionStrategy.Should().Be(ElectionStrategy.BordaCount);
    }

    [Fact]
    public void WithMode_SetsThinkingMode()
    {
        var result = _mind.WithMode(CollectiveThinkingMode.Racing);
        result.Should().BeSameAs(_mind);
        _mind.ThinkingMode.Should().Be(CollectiveThinkingMode.Racing);
    }

    [Fact]
    public void WithDecomposition_SetsDecomposedMode()
    {
        var result = _mind.WithDecomposition();
        result.Should().BeSameAs(_mind);
        _mind.ThinkingMode.Should().Be(CollectiveThinkingMode.Decomposed);
    }

    [Fact]
    public void WithDecomposition_WithConfig_SetsConfig()
    {
        var config = DecompositionConfig.QualityFirst;
        var result = _mind.WithDecomposition(config);
        result.Should().BeSameAs(_mind);
        _mind.ThinkingMode.Should().Be(CollectiveThinkingMode.Decomposed);
        _mind.DecompositionConfig.Should().BeSameAs(config);
    }

    [Fact]
    public void WithDecomposition_WithNullConfig_DoesNotOverrideExisting()
    {
        var originalConfig = _mind.DecompositionConfig;
        var result = _mind.WithDecomposition(null);
        result.Should().BeSameAs(_mind);
        _mind.DecompositionConfig.Should().BeSameAs(originalConfig);
    }

    [Fact]
    public void WithLocalFirst_SetsDecomposedWithLocalFirstConfig()
    {
        var result = _mind.WithLocalFirst();
        result.Should().BeSameAs(_mind);
        _mind.ThinkingMode.Should().Be(CollectiveThinkingMode.Decomposed);
        _mind.DecompositionConfig.PreferLocalForSimple.Should().BeTrue();
    }

    [Fact]
    public void WithQualityFirst_SetsDecomposedWithQualityConfig()
    {
        var result = _mind.WithQualityFirst();
        result.Should().BeSameAs(_mind);
        _mind.ThinkingMode.Should().Be(CollectiveThinkingMode.Decomposed);
        _mind.DecompositionConfig.PremiumForSynthesis.Should().BeTrue();
    }

    [Fact]
    public void WithMaster_SetsAndReturnsSameMind()
    {
        var result = _mind.WithMaster("Ollama");
        result.Should().BeSameAs(_mind);
    }

    [Fact]
    public void WithPathwayConfig_SetsAndReturnsSameMind()
    {
        var result = _mind.WithPathwayConfig("Ollama", PathwayTier.Local, SubGoalType.Coding);
        result.Should().BeSameAs(_mind);
    }

    [Fact]
    public void Ask_ReturnsMindOperation()
    {
        var operation = _mind.Ask("What is AI?");
        operation.Should().NotBeNull();
    }

    [Fact]
    public async Task PipelineAsync_ExecutesConfigThenOperation()
    {
        // Arrange
        var configExecuted = false;
        var config = MindOperation<VoidResult>.FromAsync((mind, _) =>
        {
            configExecuted = true;
            return Task.FromResult(VoidResult.Value);
        });
        var operation = MindOperation<string>.FromAsync((mind, _) =>
            Task.FromResult("result"));

        // Act
        var result = await _mind.PipelineAsync(config, operation);

        // Assert
        configExecuted.Should().BeTrue();
        result.Should().Be("result");
    }

    public void Dispose()
    {
        _mind.Dispose();
    }
}
