using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class ExternalKnowledgeConfigTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var config = new ExternalKnowledgeConfig();

        config.ArxivBaseUrl.Should().Be("http://export.arxiv.org/api/query");
        config.SemanticScholarBaseUrl.Should().Be("https://api.semanticscholar.org/graph/v1");
        config.MaxPapersPerQuery.Should().Be(10);
        config.RequestTimeoutSeconds.Should().Be(30);
        config.RateLimitDelayMs.Should().Be(500);
        config.EnableCaching.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithCustomValues_SetsProperties()
    {
        var config = new ExternalKnowledgeConfig(
            ArxivBaseUrl: "http://custom-arxiv.com",
            MaxPapersPerQuery: 25,
            EnableCaching: false);

        config.ArxivBaseUrl.Should().Be("http://custom-arxiv.com");
        config.MaxPapersPerQuery.Should().Be(25);
        config.EnableCaching.Should().BeFalse();
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        var a = new ExternalKnowledgeConfig(MaxPapersPerQuery: 5);
        var b = new ExternalKnowledgeConfig(MaxPapersPerQuery: 5);

        a.Should().Be(b);
    }
}
