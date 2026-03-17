using Ouroboros.Pipeline.GraphRAG.Models;

namespace Ouroboros.Tests.GraphRAG;

[Trait("Category", "Unit")]
public sealed class HybridSearchConfigTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        // Arrange & Act
        var config = HybridSearchConfig.Default;

        // Assert
        config.VectorWeight.Should().Be(0.5);
        config.SymbolicWeight.Should().Be(0.5);
        config.MaxResults.Should().Be(10);
        config.MaxHops.Should().Be(2);
        config.SimilarityThreshold.Should().Be(0.7);
        config.IncludeExplanation.Should().BeTrue();
    }

    [Fact]
    public void VectorFocused_HasHighVectorWeight()
    {
        // Arrange & Act
        var config = HybridSearchConfig.VectorFocused;

        // Assert
        config.VectorWeight.Should().Be(0.8);
        config.SymbolicWeight.Should().Be(0.2);
    }

    [Fact]
    public void SymbolicFocused_HasHighSymbolicWeight()
    {
        // Arrange & Act
        var config = HybridSearchConfig.SymbolicFocused;

        // Assert
        config.VectorWeight.Should().Be(0.2);
        config.SymbolicWeight.Should().Be(0.8);
    }

    [Fact]
    public void DeepTraversal_HasHighMaxHopsAndResults()
    {
        // Arrange & Act
        var config = HybridSearchConfig.DeepTraversal;

        // Assert
        config.MaxHops.Should().Be(5);
        config.MaxResults.Should().Be(20);
    }

    [Fact]
    public void Constructor_WithCustomValues_SetsProperties()
    {
        // Arrange & Act
        var config = new HybridSearchConfig(
            VectorWeight: 0.3,
            SymbolicWeight: 0.7,
            MaxResults: 15,
            MaxHops: 3);

        // Assert
        config.VectorWeight.Should().Be(0.3);
        config.SymbolicWeight.Should().Be(0.7);
        config.MaxResults.Should().Be(15);
        config.MaxHops.Should().Be(3);
    }

    [Fact]
    public void SimilarityThreshold_CanBeSetViaInit()
    {
        // Arrange & Act
        var config = new HybridSearchConfig() { SimilarityThreshold = 0.9 };

        // Assert
        config.SimilarityThreshold.Should().Be(0.9);
    }

    [Fact]
    public void IncludeExplanation_CanBeSetToFalse()
    {
        // Arrange & Act
        var config = new HybridSearchConfig() { IncludeExplanation = false };

        // Assert
        config.IncludeExplanation.Should().BeFalse();
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var config1 = new HybridSearchConfig(0.5, 0.5, 10, 2);
        var config2 = new HybridSearchConfig(0.5, 0.5, 10, 2);

        // Act & Assert
        config1.Should().Be(config2);
    }
}
