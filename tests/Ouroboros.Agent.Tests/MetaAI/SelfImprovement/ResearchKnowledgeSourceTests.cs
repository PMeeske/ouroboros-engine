// <copyright file="ResearchKnowledgeSourceTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net;
using FluentAssertions;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class ResearchKnowledgeSourceTests : IDisposable
{
    private readonly ResearchKnowledgeSource _sut;
    private readonly HttpClient _httpClient;

    public ResearchKnowledgeSourceTests()
    {
        _httpClient = new HttpClient(new FakeHttpHandler());
        _sut = new ResearchKnowledgeSource(httpClient: _httpClient);
    }

    public void Dispose()
    {
        _sut.Dispose();
        _httpClient.Dispose();
    }

    // ── SearchPapersAsync ──────────────────────────────────────────

    [Fact]
    public async Task SearchPapersAsync_EmptyQuery_ReturnsFailure()
    {
        var result = await _sut.SearchPapersAsync("");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Query cannot be empty");
    }

    [Fact]
    public async Task SearchPapersAsync_WhitespaceQuery_ReturnsFailure()
    {
        var result = await _sut.SearchPapersAsync("   ");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Query cannot be empty");
    }

    [Fact]
    public async Task SearchPapersAsync_NullQuery_ReturnsFailure()
    {
        var result = await _sut.SearchPapersAsync(null!);

        result.IsSuccess.Should().BeFalse();
    }

    // ── GetCitationsAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetCitationsAsync_EmptyPaperId_ReturnsFailure()
    {
        var result = await _sut.GetCitationsAsync("");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Paper ID cannot be empty");
    }

    [Fact]
    public async Task GetCitationsAsync_WhitespacePaperId_ReturnsFailure()
    {
        var result = await _sut.GetCitationsAsync("   ");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Paper ID cannot be empty");
    }

    // ── ExtractObservationsAsync ───────────────────────────────────

    [Fact]
    public async Task ExtractObservationsAsync_NullPapers_ReturnsEmpty()
    {
        var observations = await _sut.ExtractObservationsAsync(null!);

        observations.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractObservationsAsync_EmptyPapers_ReturnsEmpty()
    {
        var observations = await _sut.ExtractObservationsAsync(new List<ResearchPaper>());

        observations.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractObservationsAsync_WithPapers_ExtractsDomainPatterns()
    {
        var papers = new List<ResearchPaper>
        {
            new("1", "Transformer Architecture for NLP", "Author A", "A novel approach to NLP", "cs.CL", "url1"),
            new("2", "Transformer Models in Vision", "Author B", "Applying transformers to vision", "cs.CV", "url2"),
            new("3", "Another Transformer Study", "Author C", "Further transformer research", "cs.CL", "url3")
        };

        var observations = await _sut.ExtractObservationsAsync(papers);

        observations.Should().NotBeEmpty();
        observations.Should().Contain(o => o.Contains("cs"));
    }

    [Fact]
    public async Task ExtractObservationsAsync_WithRepeatedKeywords_ExtractsTermFrequency()
    {
        var papers = new List<ResearchPaper>
        {
            new("1", "Reinforcement Learning Approach", "A", "abstract", "cs.AI", "url1"),
            new("2", "Deep Reinforcement Learning", "B", "abstract", "cs.AI", "url2"),
            new("3", "Reinforcement Learning Survey", "C", "abstract", "cs.AI", "url3")
        };

        var observations = await _sut.ExtractObservationsAsync(papers);

        observations.Should().NotBeEmpty();
        observations.Should().Contain(o => o.Contains("reinforcement"));
    }

    // ── BuildKnowledgeGraphFactsAsync ──────────────────────────────

    [Fact]
    public async Task BuildKnowledgeGraphFactsAsync_WithPapersAndCitations_ReturnsFactsWithTypeDeclarations()
    {
        var papers = new List<ResearchPaper>
        {
            new("paper1", "Test Paper", "Author One, Author Two", "Abstract", "cs.AI", "url")
        };
        var citations = new List<CitationMetadata>
        {
            new("paper1", "Test Paper", 42, 5, new List<string> { "Ref Paper" }, new List<string>())
        };

        var facts = await _sut.BuildKnowledgeGraphFactsAsync(papers, citations);

        facts.Should().NotBeEmpty();
        facts.Should().Contain("(: Paper Type)");
        facts.Should().Contain("(: Author Type)");
        facts.Should().Contain("(: Category Type)");
        facts.Should().Contain(f => f.Contains("in_category"));
        facts.Should().Contain(f => f.Contains("authored_by"));
        facts.Should().Contain(f => f.Contains("has_citations"));
    }

    [Fact]
    public async Task BuildKnowledgeGraphFactsAsync_EmptyInputs_ReturnsTypeDeclarationsAndRules()
    {
        var facts = await _sut.BuildKnowledgeGraphFactsAsync(
            new List<ResearchPaper>(), new List<CitationMetadata>());

        facts.Should().NotBeEmpty();
        facts.Should().Contain("(: Paper Type)");
        facts.Should().Contain(f => f.Contains("transitively_cites"));
        facts.Should().Contain(f => f.Contains("related_by_citation"));
    }

    [Fact]
    public async Task BuildKnowledgeGraphFactsAsync_WithMultipleAuthors_SplitsAuthors()
    {
        var papers = new List<ResearchPaper>
        {
            new("p1", "Title", "Alice, Bob, Charlie", "Abstract", "cs.AI", "url")
        };

        var facts = await _sut.BuildKnowledgeGraphFactsAsync(papers, new List<CitationMetadata>());

        facts.Should().Contain(f => f.Contains("alice"));
        facts.Should().Contain(f => f.Contains("bob"));
        facts.Should().Contain(f => f.Contains("charlie"));
    }

    // ── Constructor ────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithCustomConfig_UsesConfig()
    {
        var config = new ExternalKnowledgeConfig(MaxPapersPerQuery: 20);
        using var source = new ResearchKnowledgeSource(config: config, httpClient: _httpClient);

        // Should not throw - basic smoke test
        source.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullConfig_UsesDefaults()
    {
        using var source = new ResearchKnowledgeSource(httpClient: _httpClient);
        source.Should().NotBeNull();
    }

    // ── Dispose ────────────────────────────────────────────────────

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        using var source = new ResearchKnowledgeSource(httpClient: _httpClient);

        var act = () =>
        {
            source.Dispose();
            source.Dispose();
        };

        act.Should().NotThrow();
    }

    /// <summary>
    /// Fake HTTP handler that returns empty responses to avoid real network calls.
    /// </summary>
    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<feed xmlns=\"http://www.w3.org/2005/Atom\"></feed>")
            };
            return Task.FromResult(response);
        }
    }
}
