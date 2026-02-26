namespace Ouroboros.Tests.Pipeline.GraphRAG.Models;

using Ouroboros.Pipeline.GraphRAG.Models;

[Trait("Category", "Unit")]
public class HybridSearchResultTests
{
    [Fact]
    public void Empty_HasNoMatchesInferencesOrChain()
    {
        var result = HybridSearchResult.Empty;

        result.Matches.Should().BeEmpty();
        result.Inferences.Should().BeEmpty();
        result.ReasoningChain.Should().BeEmpty();
    }

    [Fact]
    public void TotalRelevance_SumsMatchRelevances()
    {
        var matches = new List<SearchMatch>
        {
            new("e1", "A", "T", "C", 0.5, 0.6, 0.4),
            new("e2", "B", "T", "C", 0.3, 0.4, 0.2),
        };

        var result = new HybridSearchResult(matches, [], []);

        result.TotalRelevance.Should().BeApproximately(0.8, 0.001);
    }

    [Fact]
    public void TotalRelevance_ReturnsZeroWhenEmpty()
    {
        HybridSearchResult.Empty.TotalRelevance.Should().Be(0.0);
    }

    [Fact]
    public void TopMatches_ReturnsTopNByRelevance()
    {
        var matches = new List<SearchMatch>
        {
            new("e1", "A", "T", "C", 0.3, 0.3, 0.3),
            new("e2", "B", "T", "C", 0.9, 0.9, 0.9),
            new("e3", "C", "T", "C", 0.5, 0.5, 0.5),
        };

        var result = new HybridSearchResult(matches, [], []);
        var top = result.TopMatches(2).ToList();

        top.Should().HaveCount(2);
        top[0].Relevance.Should().Be(0.9);
        top[1].Relevance.Should().Be(0.5);
    }
}
