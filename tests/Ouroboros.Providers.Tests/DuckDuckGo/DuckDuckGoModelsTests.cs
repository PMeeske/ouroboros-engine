using Ouroboros.Providers.DuckDuckGo;

namespace Ouroboros.Tests.DuckDuckGo;

[Trait("Category", "Unit")]
public sealed class DuckDuckGoModelsTests
{
    [Fact]
    public void SearchResult_SetsProperties()
    {
        var result = new DuckDuckGoSearchResult
        {
            Title = "Test",
            Url = "https://example.com",
            Snippet = "A snippet"
        };

        result.Title.Should().Be("Test");
        result.Url.Should().Be("https://example.com");
        result.Snippet.Should().Be("A snippet");
    }

    [Fact]
    public void NewsResult_SetsProperties()
    {
        var result = new DuckDuckGoNewsResult
        {
            Title = "News",
            Url = "https://news.com",
            Source = "BBC"
        };

        result.Title.Should().Be("News");
        result.Source.Should().Be("BBC");
    }

    [Fact]
    public void InstantAnswer_DefaultRelatedTopics()
    {
        var answer = new DuckDuckGoInstantAnswer();
        answer.RelatedTopics.Should().BeEmpty();
    }

    [Fact]
    public void RelatedTopic_SetsProperties()
    {
        var topic = new DuckDuckGoRelatedTopic { Text = "Topic", Url = "https://t.com" };

        topic.Text.Should().Be("Topic");
        topic.Url.Should().Be("https://t.com");
    }
}
