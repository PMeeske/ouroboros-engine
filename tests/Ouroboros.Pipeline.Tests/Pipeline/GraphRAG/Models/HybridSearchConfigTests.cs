namespace Ouroboros.Tests.Pipeline.GraphRAG.Models;

using Ouroboros.Pipeline.GraphRAG.Models;

[Trait("Category", "Unit")]
public class HybridSearchConfigTests
{
    [Fact]
    public void Default_HasEqualWeights()
    {
        var config = HybridSearchConfig.Default;

        config.VectorWeight.Should().Be(0.5);
        config.SymbolicWeight.Should().Be(0.5);
        config.MaxResults.Should().Be(10);
        config.MaxHops.Should().Be(2);
    }

    [Fact]
    public void VectorFocused_FavorsVectorWeight()
    {
        var config = HybridSearchConfig.VectorFocused;

        config.VectorWeight.Should().Be(0.8);
        config.SymbolicWeight.Should().Be(0.2);
    }

    [Fact]
    public void SymbolicFocused_FavorsSymbolicWeight()
    {
        var config = HybridSearchConfig.SymbolicFocused;

        config.VectorWeight.Should().Be(0.2);
        config.SymbolicWeight.Should().Be(0.8);
    }

    [Fact]
    public void DeepTraversal_HasHigherHopsAndResults()
    {
        var config = HybridSearchConfig.DeepTraversal;

        config.MaxHops.Should().Be(5);
        config.MaxResults.Should().Be(20);
    }

    [Fact]
    public void SimilarityThreshold_DefaultsToPointSeven()
    {
        var config = HybridSearchConfig.Default;
        config.SimilarityThreshold.Should().Be(0.7);
    }

    [Fact]
    public void IncludeExplanation_DefaultsToTrue()
    {
        var config = HybridSearchConfig.Default;
        config.IncludeExplanation.Should().BeTrue();
    }

    [Fact]
    public void WithExpression_CanOverrideSimilarityThreshold()
    {
        var config = HybridSearchConfig.Default with { SimilarityThreshold = 0.9 };
        config.SimilarityThreshold.Should().Be(0.9);
    }
}
