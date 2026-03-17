// <copyright file="ExternalKnowledgeConfigTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class ExternalKnowledgeConfigTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultValues()
    {
        var config = new ExternalKnowledgeConfig();

        config.ArxivBaseUrl.Should().Be("http://export.arxiv.org/api/query");
        config.SemanticScholarBaseUrl.Should().Be("https://api.semanticscholar.org/graph/v1");
        config.MaxPapersPerQuery.Should().Be(10);
        config.RequestTimeoutSeconds.Should().Be(30);
        config.RateLimitDelayMs.Should().Be(500);
        config.EnableCaching.Should().BeTrue();
        config.CacheExpiration.Should().Be(default(TimeSpan));
    }

    [Fact]
    public void Constructor_WithCustomValues_OverridesDefaults()
    {
        var config = new ExternalKnowledgeConfig(
            ArxivBaseUrl: "http://custom.arxiv.org",
            SemanticScholarBaseUrl: "http://custom.s2.org",
            MaxPapersPerQuery: 20,
            RequestTimeoutSeconds: 60,
            RateLimitDelayMs: 1000,
            EnableCaching: false,
            CacheExpiration: TimeSpan.FromHours(2));

        config.ArxivBaseUrl.Should().Be("http://custom.arxiv.org");
        config.SemanticScholarBaseUrl.Should().Be("http://custom.s2.org");
        config.MaxPapersPerQuery.Should().Be(20);
        config.RequestTimeoutSeconds.Should().Be(60);
        config.RateLimitDelayMs.Should().Be(1000);
        config.EnableCaching.Should().BeFalse();
        config.CacheExpiration.Should().Be(TimeSpan.FromHours(2));
    }

    [Fact]
    public void With_CanModifySingleProperty()
    {
        var config = new ExternalKnowledgeConfig();

        var modified = config with { MaxPapersPerQuery = 50 };

        modified.MaxPapersPerQuery.Should().Be(50);
        modified.ArxivBaseUrl.Should().Be(config.ArxivBaseUrl);
    }

    [Fact]
    public void Equality_SameDefaults_AreEqual()
    {
        var a = new ExternalKnowledgeConfig();
        var b = new ExternalKnowledgeConfig();

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new ExternalKnowledgeConfig();
        var b = new ExternalKnowledgeConfig(MaxPapersPerQuery: 99);

        a.Should().NotBe(b);
    }
}
