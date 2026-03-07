using Ouroboros.Providers.Firecrawl;

namespace Ouroboros.Tests.Firecrawl;

[Trait("Category", "Unit")]
public sealed class FirecrawlModelsTests
{
    [Fact]
    public void ScrapeResult_SetsProperties()
    {
        var result = new FirecrawlScrapeResult
        {
            Url = "https://example.com",
            Title = "Example",
            Markdown = "# Hello"
        };

        result.Url.Should().Be("https://example.com");
        result.Title.Should().Be("Example");
        result.Markdown.Should().Be("# Hello");
    }

    [Fact]
    public void Metadata_SetsProperties()
    {
        var meta = new FirecrawlMetadata
        {
            Title = "T",
            Description = "D",
            Language = "en",
            SourceUrl = "https://src.com",
            StatusCode = 200
        };

        meta.StatusCode.Should().Be(200);
    }

    [Fact]
    public void ScrapeOptions_DefaultFormats()
    {
        var options = new FirecrawlScrapeOptions();
        options.Formats.Should().ContainSingle().Which.Should().Be("markdown");
    }

    [Fact]
    public void CrawlOptions_Defaults()
    {
        var options = new FirecrawlCrawlOptions();
        options.MaxPages.Should().Be(50);
        options.MaxDepth.Should().Be(3);
    }

    [Fact]
    public void CrawlStatus_DefaultResults()
    {
        var status = new FirecrawlCrawlStatus { JobId = "j1", Status = "scraping" };
        status.Results.Should().BeEmpty();
    }

    [Fact]
    public void SearchResult_SetsProperties()
    {
        var result = new FirecrawlSearchResult { Url = "https://s.com", Title = "S" };
        result.Url.Should().Be("https://s.com");
    }
}
