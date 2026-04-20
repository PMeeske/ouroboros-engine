#pragma warning disable CA2000 // Test file - ownership is managed by test lifecycle
using FluentAssertions;
using Ouroboros.Providers;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class MindDslTests
{
    [Fact]
    public void Think_ReturnsNonNullOperation()
    {
        var op = MindDsl.Think("test prompt");
        op.Should().NotBeNull();
    }

    [Fact]
    public void Generate_ReturnsNonNullOperation()
    {
        var op = MindDsl.Generate("test prompt");
        op.Should().NotBeNull();
    }

    [Fact]
    public void Race_ReturnsNonNullOperation()
    {
        var op = MindDsl.Race("test prompt");
        op.Should().NotBeNull();
    }

    [Fact]
    public void Ensemble_ReturnsNonNullOperation()
    {
        var op = MindDsl.Ensemble("test prompt");
        op.Should().NotBeNull();
    }

    [Fact]
    public void Sequential_ReturnsNonNullOperation()
    {
        var op = MindDsl.Sequential("test prompt");
        op.Should().NotBeNull();
    }

    [Fact]
    public void Decompose_ReturnsNonNullOperation()
    {
        var op = MindDsl.Decompose("test prompt");
        op.Should().NotBeNull();
    }

    [Fact]
    public void Decompose_WithConfig_ReturnsNonNullOperation()
    {
        var config = DecompositionConfig.QualityFirst;
        var op = MindDsl.Decompose("test prompt", config);
        op.Should().NotBeNull();
    }

    [Fact]
    public void DecomposeLocalFirst_ReturnsNonNullOperation()
    {
        var op = MindDsl.DecomposeLocalFirst("test prompt");
        op.Should().NotBeNull();
    }

    [Fact]
    public void DecomposeQualityFirst_ReturnsNonNullOperation()
    {
        var op = MindDsl.DecomposeQualityFirst("test prompt");
        op.Should().NotBeNull();
    }

    [Fact]
    public void SetMaster_ReturnsVoidOperation()
    {
        var op = MindDsl.SetMaster("pathway1");
        op.Should().NotBeNull();
    }

    [Fact]
    public void UseElection_ReturnsVoidOperation()
    {
        var op = MindDsl.UseElection(ElectionStrategy.BordaCount);
        op.Should().NotBeNull();
    }

    [Fact]
    public void UseMode_ReturnsVoidOperation()
    {
        var op = MindDsl.UseMode(CollectiveThinkingMode.Ensemble);
        op.Should().NotBeNull();
    }

    [Fact]
    public void AddPathway_ReturnsVoidOperation()
    {
        var op = MindDsl.AddPathway("test", ChatEndpointType.OpenAI);
        op.Should().NotBeNull();
    }

    [Fact]
    public void UseDecomposition_ReturnsVoidOperation()
    {
        var op = MindDsl.UseDecomposition(DecompositionConfig.Default);
        op.Should().NotBeNull();
    }

    [Fact]
    public void ConfigurePathway_ReturnsVoidOperation()
    {
        var op = MindDsl.ConfigurePathway("test", PathwayTier.CloudPremium, SubGoalType.Coding);
        op.Should().NotBeNull();
    }

    [Fact]
    public void GetOptimizations_ReturnsNonNullOperation()
    {
        var op = MindDsl.GetOptimizations();
        op.Should().NotBeNull();
    }

    [Fact]
    public void GetStatus_ReturnsNonNullOperation()
    {
        var op = MindDsl.GetStatus();
        op.Should().NotBeNull();
    }

    [Fact]
    public void GetHealthyPathways_ReturnsNonNullOperation()
    {
        var op = MindDsl.GetHealthyPathways();
        op.Should().NotBeNull();
    }

    [Fact]
    public async Task Sequence_ExecutesInOrder()
    {
        // Arrange
        var order = new List<int>();
        var op1 = MindOperation<int>.FromAsync((_, _) => { order.Add(1); return Task.FromResult(1); });
        var op2 = MindOperation<int>.FromAsync((_, _) => { order.Add(2); return Task.FromResult(2); });
        var op3 = MindOperation<int>.FromAsync((_, _) => { order.Add(3); return Task.FromResult(3); });
        var sequence = MindDsl.Sequence(op1, op2, op3);

        using var mind = CollectiveMindFactory.CreateLocal();

        // Act
        var result = await mind.RunAsync(sequence);

        // Assert
        order.Should().ContainInOrder(1, 2, 3);
        result.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public async Task Parallel_ExecutesAllOperations()
    {
        // Arrange
        var op1 = MindOperation<int>.FromAsync((_, _) => Task.FromResult(10));
        var op2 = MindOperation<int>.FromAsync((_, _) => Task.FromResult(20));
        var parallel = MindDsl.Parallel(op1, op2);

        using var mind = CollectiveMindFactory.CreateLocal();

        // Act
        var result = await mind.RunAsync(parallel);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(10);
        result.Should().Contain(20);
    }

    [Fact]
    public async Task WithFallback_WhenPrimaryFails_UsesFallback()
    {
        // Arrange
        var primary = MindOperation<string>.FromAsync((_, _) =>
            throw new InvalidOperationException("primary failed"));
        var fallback = MindOperation<string>.FromAsync((_, _) =>
            Task.FromResult("fallback result"));
        var op = MindDsl.WithFallback(primary, fallback);

        using var mind = CollectiveMindFactory.CreateLocal();

        // Act
        var result = await mind.RunAsync(op);

        // Assert
        result.Should().Be("fallback result");
    }

    [Fact]
    public async Task WithFallback_WhenPrimarySucceeds_ReturnsPrimary()
    {
        // Arrange
        var primary = MindOperation<string>.FromAsync((_, _) =>
            Task.FromResult("primary result"));
        var fallback = MindOperation<string>.FromAsync((_, _) =>
            Task.FromResult("fallback"));
        var op = MindDsl.WithFallback(primary, fallback);

        using var mind = CollectiveMindFactory.CreateLocal();

        // Act
        var result = await mind.RunAsync(op);

        // Assert
        result.Should().Be("primary result");
    }

    [Fact]
    public async Task Map_TransformsResult()
    {
        // Arrange
        var op = MindOperation<int>.FromAsync((_, _) => Task.FromResult(42));
        var mapped = MindDsl.Map(op, x => x.ToString());

        using var mind = CollectiveMindFactory.CreateLocal();

        // Act
        var result = await mind.RunAsync(mapped);

        // Assert
        result.Should().Be("42");
    }

    [Fact]
    public async Task Bind_ChainsOperations()
    {
        // Arrange
        var op = MindOperation<int>.FromAsync((_, _) => Task.FromResult(5));
        var chained = MindDsl.Bind(op, x =>
            MindOperation<string>.FromAsync((_, _) => Task.FromResult($"Value: {x}")));

        using var mind = CollectiveMindFactory.CreateLocal();

        // Act
        var result = await mind.RunAsync(chained);

        // Assert
        result.Should().Be("Value: 5");
    }

    [Fact]
    public async Task UseMode_ExecutesOnMind()
    {
        // Arrange
        using var mind = CollectiveMindFactory.CreateLocal();
        var op = MindDsl.UseMode(CollectiveThinkingMode.Ensemble);

        // Act
        await mind.RunAsync(op);

        // Assert
        mind.ThinkingMode.Should().Be(CollectiveThinkingMode.Ensemble);
    }

    [Fact]
    public async Task UseElection_ExecutesOnMind()
    {
        // Arrange
        using var mind = CollectiveMindFactory.CreateLocal();
        var op = MindDsl.UseElection(ElectionStrategy.InstantRunoff);

        // Act
        await mind.RunAsync(op);

        // Assert
        mind.ElectionStrategy.Should().Be(ElectionStrategy.InstantRunoff);
    }

    [Fact]
    public async Task GetStatus_ReturnsStatusString()
    {
        // Arrange
        using var mind = CollectiveMindFactory.CreateLocal();
        var op = MindDsl.GetStatus();

        // Act
        var status = await mind.RunAsync(op);

        // Assert
        status.Should().Contain("Collective Mind Status");
    }

    [Fact]
    public async Task GetOptimizations_ReturnsEmptyList()
    {
        // Arrange
        using var mind = CollectiveMindFactory.CreateLocal();
        var op = MindDsl.GetOptimizations();

        // Act
        var suggestions = await mind.RunAsync(op);

        // Assert
        suggestions.Should().BeEmpty();
    }
}
