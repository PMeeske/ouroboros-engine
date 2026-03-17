using Ouroboros.Pipeline.GraphRAG.Models;

namespace Ouroboros.Tests.GraphRAG;

[Trait("Category", "Unit")]
public sealed class HybridSearchResultTests
{
    private static SearchMatch CreateMatch(string id, string name, double relevance) =>
        new(id, name, "Person", "Content for " + name, relevance, relevance * 0.9, relevance * 0.8);

    #region Empty

    [Fact]
    public void Empty_HasNoMatchesInferencesOrReasoningChain()
    {
        // Arrange & Act
        var result = HybridSearchResult.Empty;

        // Assert
        result.Matches.Should().BeEmpty();
        result.Inferences.Should().BeEmpty();
        result.ReasoningChain.Should().BeEmpty();
    }

    #endregion

    #region TotalRelevance

    [Fact]
    public void TotalRelevance_WithNoMatches_ReturnsZero()
    {
        // Arrange & Act
        var result = HybridSearchResult.Empty;

        // Assert
        result.TotalRelevance.Should().Be(0.0);
    }

    [Fact]
    public void TotalRelevance_WithSingleMatch_ReturnsThatRelevance()
    {
        // Arrange
        var matches = new List<SearchMatch> { CreateMatch("e1", "Alice", 0.9) };
        var result = new HybridSearchResult(matches, [], []);

        // Act & Assert
        result.TotalRelevance.Should().Be(0.9);
    }

    [Fact]
    public void TotalRelevance_WithMultipleMatches_ReturnsSumOfRelevances()
    {
        // Arrange
        var matches = new List<SearchMatch>
        {
            CreateMatch("e1", "Alice", 0.9),
            CreateMatch("e2", "Bob", 0.7),
            CreateMatch("e3", "Charlie", 0.5)
        };
        var result = new HybridSearchResult(matches, [], []);

        // Act & Assert
        result.TotalRelevance.Should().BeApproximately(2.1, 0.001);
    }

    #endregion

    #region TopMatches

    [Fact]
    public void TopMatches_ReturnsTopNByRelevanceDescending()
    {
        // Arrange
        var matches = new List<SearchMatch>
        {
            CreateMatch("e1", "Alice", 0.5),
            CreateMatch("e2", "Bob", 0.9),
            CreateMatch("e3", "Charlie", 0.7)
        };
        var result = new HybridSearchResult(matches, [], []);

        // Act
        var top = result.TopMatches(2).ToList();

        // Assert
        top.Should().HaveCount(2);
        top[0].EntityName.Should().Be("Bob");
        top[1].EntityName.Should().Be("Charlie");
    }

    [Fact]
    public void TopMatches_WithNGreaterThanCount_ReturnsAll()
    {
        // Arrange
        var matches = new List<SearchMatch>
        {
            CreateMatch("e1", "Alice", 0.9),
            CreateMatch("e2", "Bob", 0.7)
        };
        var result = new HybridSearchResult(matches, [], []);

        // Act
        var top = result.TopMatches(10).ToList();

        // Assert
        top.Should().HaveCount(2);
    }

    [Fact]
    public void TopMatches_WithZero_ReturnsEmpty()
    {
        // Arrange
        var matches = new List<SearchMatch> { CreateMatch("e1", "Alice", 0.9) };
        var result = new HybridSearchResult(matches, [], []);

        // Act
        var top = result.TopMatches(0).ToList();

        // Assert
        top.Should().BeEmpty();
    }

    [Fact]
    public void TopMatches_WithNoMatches_ReturnsEmpty()
    {
        // Arrange
        var result = HybridSearchResult.Empty;

        // Act
        var top = result.TopMatches(5).ToList();

        // Assert
        top.Should().BeEmpty();
    }

    #endregion

    #region Constructor

    [Fact]
    public void Constructor_WithInferences_SetsInferences()
    {
        // Arrange
        var inferences = new List<Inference>
        {
            new(new List<string> { "A is B" }, "Conclusion", 0.9, "Rule1")
        };

        // Act
        var result = new HybridSearchResult([], inferences, []);

        // Assert
        result.Inferences.Should().HaveCount(1);
        result.Inferences[0].Conclusion.Should().Be("Conclusion");
    }

    [Fact]
    public void Constructor_WithReasoningChain_SetsReasoningChain()
    {
        // Arrange
        var chain = new List<ReasoningChainStep>
        {
            new(1, "Traverse", "Step description", new List<string> { "e1" })
        };

        // Act
        var result = new HybridSearchResult([], [], chain);

        // Assert
        result.ReasoningChain.Should().HaveCount(1);
        result.ReasoningChain[0].Operation.Should().Be("Traverse");
    }

    #endregion
}
