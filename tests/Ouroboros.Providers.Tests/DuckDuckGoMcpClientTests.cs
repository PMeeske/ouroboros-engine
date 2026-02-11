// <copyright file="DuckDuckGoMcpClientTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Providers.DuckDuckGo;

namespace Ouroboros.Tests.Tests.DuckDuckGo;

/// <summary>
/// Tests for DuckDuckGoMcpClient operations.
/// </summary>
public sealed class DuckDuckGoMcpClientTests : IDisposable
{
    private readonly DuckDuckGoMcpClientOptions _options;

    public DuckDuckGoMcpClientTests()
    {
        _options = new DuckDuckGoMcpClientOptions
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    [Fact]
    public void Constructor_DefaultOptions_DoesNotThrow()
    {
        var client = new DuckDuckGoMcpClient();
        client.Should().NotBeNull();
    }

    [Fact]
    public void Options_Default_IsValid()
    {
        _options.IsValid().Should().BeTrue();
    }

    [Fact]
    public void Options_NoApiKeyRequired()
    {
        new DuckDuckGoMcpClientOptions().IsValid().Should().BeTrue();
    }

    [Fact]
    public async Task InstantAnswerAsync_ParsesAnswer()
    {
        var json = """
        {
            "Heading": "DuckDuckGo",
            "AbstractText": "DuckDuckGo is an internet search engine.",
            "AbstractSource": "Wikipedia",
            "AbstractURL": "https://en.wikipedia.org/wiki/DuckDuckGo",
            "Image": "https://duckduckgo.com/i/logo.png",
            "Answer": "",
            "AnswerType": "",
            "Definition": "",
            "RelatedTopics": [
                { "Text": "Privacy search engine", "FirstURL": "https://example.com/privacy" }
            ]
        }
        """;

        var handler = new MockHttpHandler(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler);
        var client = new DuckDuckGoMcpClient(_options, httpClient);

        var result = await client.InstantAnswerAsync("DuckDuckGo");

        result.IsSuccess.Should().BeTrue();
        result.Value.Heading.Should().Be("DuckDuckGo");
        result.Value.AbstractText.Should().Contain("search engine");
        result.Value.AbstractSource.Should().Be("Wikipedia");
        result.Value.RelatedTopics.Should().HaveCount(1);
    }

    [Fact]
    public async Task InstantAnswerAsync_ServerError_ReturnsError()
    {
        var handler = new MockHttpHandler(HttpStatusCode.ServiceUnavailable, "");
        using var httpClient = new HttpClient(handler);
        var client = new DuckDuckGoMcpClient(_options, httpClient);

        var result = await client.InstantAnswerAsync("test");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task SearchAsync_ParsesHtmlResults()
    {
        var html = """
        <html><body>
        <div class="result results_links results_links_deep web-result">
            <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Fpage1&amp;rut=abc">
                Example Page 1
            </a>
            <a class="result__snippet">This is a snippet for page 1</a>
        </div>
        <div class="result results_links results_links_deep web-result">
            <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Fpage2&amp;rut=def">
                Example Page 2
            </a>
            <a class="result__snippet">This is a snippet for page 2</a>
        </div>
        </body></html>
        """;

        var handler = new MockHttpHandler(HttpStatusCode.OK, html);
        using var httpClient = new HttpClient(handler);
        var client = new DuckDuckGoMcpClient(_options, httpClient);

        var result = await client.SearchAsync("example query");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Url.Should().Be("https://example.com/page1");
        result.Value[0].Title.Should().Contain("Example Page 1");
        result.Value[0].Snippet.Should().Contain("snippet for page 1");
    }

    [Fact]
    public async Task SearchAsync_EmptyResults_ReturnsEmptyList()
    {
        var html = "<html><body><div class='no-results'>No results found</div></body></html>";
        var handler = new MockHttpHandler(HttpStatusCode.OK, html);
        using var httpClient = new HttpClient(handler);
        var client = new DuckDuckGoMcpClient(_options, httpClient);

        var result = await client.SearchAsync("xyznonexistentquery123");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchNewsAsync_Success()
    {
        var html = """
        <html><body>
        <div class="result results_links results_links_deep web-result">
            <a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fnews.example.com%2Farticle1&amp;rut=abc">
                Breaking News Article
            </a>
            <a class="result__snippet">News snippet content</a>
        </div>
        </body></html>
        """;

        var handler = new MockHttpHandler(HttpStatusCode.OK, html);
        using var httpClient = new HttpClient(handler);
        var client = new DuckDuckGoMcpClient(_options, httpClient);

        var result = await client.SearchNewsAsync("breaking news");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Title.Should().Contain("Breaking News");
    }

    [Fact]
    public async Task SearchAsync_RespectsMaxResults()
    {
        var html = """
        <html><body>
        <div class="result"><a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fa.com">A</a><a class="result__snippet">s1</a></div>
        <div class="result"><a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fb.com">B</a><a class="result__snippet">s2</a></div>
        <div class="result"><a class="result__a" href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fc.com">C</a><a class="result__snippet">s3</a></div>
        </body></html>
        """;

        var handler = new MockHttpHandler(HttpStatusCode.OK, html);
        using var httpClient = new HttpClient(handler);
        var client = new DuckDuckGoMcpClient(_options, httpClient);

        var result = await client.SearchAsync("test", maxResults: 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCountLessThanOrEqualTo(2);
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
                Content = new StringContent(_responseBody, Encoding.UTF8, "text/html")
            });
        }
    }
}
