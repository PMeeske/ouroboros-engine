#pragma warning disable CA2000 // Test file - ownership is managed by test lifecycle
using FluentAssertions;
using Ouroboros.Providers;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class CollectiveMindFactoryTests
{
    [Fact]
    public void CreateBalanced_ReturnsCollectiveMind()
    {
        // Act
        using var mind = CollectiveMindFactory.CreateBalanced();

        // Assert
        mind.Should().NotBeNull();
        mind.ThinkingMode.Should().Be(CollectiveThinkingMode.Adaptive);
    }

    [Fact]
    public void CreateFast_ReturnsCollectiveWithRacingMode()
    {
        // Act
        using var mind = CollectiveMindFactory.CreateFast();

        // Assert
        mind.Should().NotBeNull();
        mind.ThinkingMode.Should().Be(CollectiveThinkingMode.Racing);
    }

    [Fact]
    public void CreatePremium_ReturnsCollectiveWithEnsembleMode()
    {
        // Act
        using var mind = CollectiveMindFactory.CreatePremium();

        // Assert
        mind.Should().NotBeNull();
        mind.ThinkingMode.Should().Be(CollectiveThinkingMode.Ensemble);
    }

    [Fact]
    public void CreateBudget_ReturnsCollectiveWithSequentialMode()
    {
        // Act
        using var mind = CollectiveMindFactory.CreateBudget();

        // Assert
        mind.Should().NotBeNull();
        mind.ThinkingMode.Should().Be(CollectiveThinkingMode.Sequential);
    }

    [Fact]
    public void CreateLocal_ReturnsCollectiveWithSingleOllamaPathway()
    {
        // Act
        using var mind = CollectiveMindFactory.CreateLocal();

        // Assert
        mind.Should().NotBeNull();
        mind.ThinkingMode.Should().Be(CollectiveThinkingMode.Sequential);
        mind.Pathways.Should().HaveCount(1);
        mind.Pathways[0].Name.Should().Be("Ollama");
    }

    [Fact]
    public void CreateLocal_WithCustomModel_UsesProvidedModel()
    {
        // Act
        using var mind = CollectiveMindFactory.CreateLocal("llama3");

        // Assert
        mind.Should().NotBeNull();
        mind.Pathways.Should().HaveCount(1);
    }

    [Fact]
    public void CreateSingle_ReturnsCollectiveWithOnePathway()
    {
        // Act
        using var mind = CollectiveMindFactory.CreateSingle(
            "TestProvider",
            ChatEndpointType.OllamaLocal,
            "llama3");

        // Assert
        mind.Should().NotBeNull();
        mind.ThinkingMode.Should().Be(CollectiveThinkingMode.Sequential);
        mind.Pathways.Should().HaveCount(1);
        mind.Pathways[0].Name.Should().Be("TestProvider");
    }

    [Fact]
    public void CreateDecomposed_ReturnsCollectiveWithDecomposedMode()
    {
        // Act
        using var mind = CollectiveMindFactory.CreateDecomposed();

        // Assert
        mind.Should().NotBeNull();
        mind.ThinkingMode.Should().Be(CollectiveThinkingMode.Decomposed);
        mind.DecompositionConfig.Should().NotBeNull();
    }

    [Fact]
    public void CreateLocalFirstDecomposed_ReturnsDecomposedWithLocalFirst()
    {
        // Act
        using var mind = CollectiveMindFactory.CreateLocalFirstDecomposed();

        // Assert
        mind.Should().NotBeNull();
        mind.ThinkingMode.Should().Be(CollectiveThinkingMode.Decomposed);
        mind.DecompositionConfig.PreferLocalForSimple.Should().BeTrue();
    }

    [Fact]
    public void CreateHybrid_WithPathways_ReturnsDecomposedMind()
    {
        // Arrange
        var pathways = new[]
        {
            ("Local", ChatEndpointType.OllamaLocal, "llama3", PathwayTier.Local),
        };

        // Act
        using var mind = CollectiveMindFactory.CreateHybrid(pathways);

        // Assert
        mind.Should().NotBeNull();
        mind.ThinkingMode.Should().Be(CollectiveThinkingMode.Decomposed);
        mind.Pathways.Should().HaveCount(1);
    }

    [Fact]
    public void CreateFromConfig_WithEmbeddingModel_DefaultsToFallbackModel()
    {
        // Act - embedding models should be rejected and default used
        using var mind = CollectiveMindFactory.CreateFromConfig("nomic-embed-text");

        // Assert
        mind.Should().NotBeNull();
        mind.ThinkingMode.Should().Be(CollectiveThinkingMode.Sequential);
    }

    [Fact]
    public void CreateBalanced_IncludesOllamaLocal()
    {
        // Act
        using var mind = CollectiveMindFactory.CreateBalanced();

        // Assert
        mind.Pathways.Should().Contain(p => p.Name == "Ollama");
    }
}
