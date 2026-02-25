// <copyright file="FirecrawlMcpClientTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Providers.Firecrawl;

namespace Ouroboros.Tests.Tests.Firecrawl;

/// <summary>
/// Tests for FirecrawlMcpClient operations.
/// </summary>
public sealed class FirecrawlMcpClientTests : IDisposable
{
    private readonly FirecrawlMcpClientOptions _options;

    public FirecrawlMcpClientTests()
    {
        _options = new FirecrawlMcpClientOptions
        {
            ApiKey = "fc-test-key",
            BaseUrl = "https://api.firecrawl.dev",
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    [Fact]
    public void Constructor_ValidOptions_DoesNotThrow()
    {
        var client = new FirecrawlMcpClient(_options, new HttpClient());
        client.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_MissingApiKey_Throws()
    {
        var invalid = new FirecrawlMcpClientOptions { ApiKey = null };
        // Clear env var for test
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FIRECRAWL_API_KEY")))
        {
            var act = () => new FirecrawlMcpClient(invalid, new HttpClient());
            act.Should().Throw<ArgumentException>();
        }
    }

    [Fact]
    public void Options_WithApiKey_IsValid()
    {
        _options.IsValid().Should().BeTrue();
    }

    [Fact]
    public async Task ScrapeAsync_ParsesResult()
    {
        var json = """
        {
            "data": {
                "url": "https://example.com",
                "markdown": "# Hello World",
                "metadata": {
                    "title": "Example",
                    "description": "Test page",
                    "language": "en",
                    "sourceURL": "https://example.com",
                    "statusCode": 200
                }
            }
        }
        """;

        var handler = new MockHttpHandler(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.firecrawl.dev") };
        var client = new FirecrawlMcpClient(_options, httpClient);

        var result = await client.ScrapeAsync("https://example.com");

        result.IsSuccess.Should().BeTrue();
        result.Value.Markdown.Should().Contain("Hello World");
        result.Value.Metadata!.Title.Should().Be("Example");
        result.Value.Metadata.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task ScrapeAsync_WithOptions_SendsRequest()
    {
        var handler = new MockHttpHandler(HttpStatusCode.OK, """{"data":{"url":"https://example.com","markdown":"content"}}""");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.firecrawl.dev") };
        var client = new FirecrawlMcpClient(_options, httpClient);

        var scrapeOptions = new FirecrawlScrapeOptions
        {
            Formats = ["markdown", "html"],
            ExcludeTags = ["nav", "footer"],
            WaitForDynamic = true
        };

        var result = await client.ScrapeAsync("https://example.com", scrapeOptions);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CrawlAsync_ReturnsJobId()
    {
        var handler = new MockHttpHandler(HttpStatusCode.OK, """{"id":"crawl-job-123"}""");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.firecrawl.dev") };
        var client = new FirecrawlMcpClient(_options, httpClient);

        var result = await client.CrawlAsync("https://docs.example.com");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("crawl-job-123");
    }

    [Fact]
    public async Task GetCrawlStatusAsync_ParsesStatus()
    {
        var json = """
        {
            "status": "completed",
            "completed": 10,
            "total": 10,
            "data": [
                { "url": "https://example.com/page1", "markdown": "Page 1 content" }
            ]
        }
        """;

        var handler = new MockHttpHandler(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.firecrawl.dev") };
        var client = new FirecrawlMcpClient(_options, httpClient);

        var result = await client.GetCrawlStatusAsync("crawl-123");

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("completed");
        result.Value.PagesScraped.Should().Be(10);
        result.Value.Results.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchAsync_ParsesResults()
    {
        var json = """
        {
            "data": [
                { "url": "https://result1.com", "title": "Result 1", "description": "Desc 1", "markdown": "Content 1" },
                { "url": "https://result2.com", "title": "Result 2", "description": "Desc 2", "markdown": "Content 2" }
            ]
        }
        """;

        var handler = new MockHttpHandler(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.firecrawl.dev") };
        var client = new FirecrawlMcpClient(_options, httpClient);

        var result = await client.SearchAsync("test query", 5);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Title.Should().Be("Result 1");
    }

    [Fact]
    public async Task MapAsync_ReturnsUrls()
    {
        var json = """{"links":["https://example.com/","https://example.com/about","https://example.com/contact"]}""";

        var handler = new MockHttpHandler(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.firecrawl.dev") };
        var client = new FirecrawlMcpClient(_options, httpClient);

        var result = await client.MapAsync("https://example.com");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task ExtractAsync_ReturnsJson()
    {
        var responseJson = """{"data":{"name":"Test","price":99.99}}""";
        var handler = new MockHttpHandler(HttpStatusCode.OK, responseJson);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.firecrawl.dev") };
        var client = new FirecrawlMcpClient(_options, httpClient);

        var schema = """{"type":"object","properties":{"name":{"type":"string"},"price":{"type":"number"}}}""";
        var result = await client.ExtractAsync("https://shop.example.com/product/1", schema);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("name");
    }

    [Fact]
    public async Task ScrapeAsync_ServerError_ReturnsError()
    {
        var handler = new MockHttpHandler(HttpStatusCode.InternalServerError, """{"error":"rate limited"}""");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.firecrawl.dev") };
        var client = new FirecrawlMcpClient(_options, httpClient);

        var result = await client.ScrapeAsync("https://example.com");

        result.IsSuccess.Should().BeFalse();
    }

    public void Dispose() { }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public MockHttpHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
