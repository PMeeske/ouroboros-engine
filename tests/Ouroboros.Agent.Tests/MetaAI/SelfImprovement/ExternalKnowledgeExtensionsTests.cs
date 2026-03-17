// <copyright file="ExternalKnowledgeExtensionsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class ExternalKnowledgeExtensionsTests
{
    private readonly Mock<IHypothesisEngine> _hypothesisEngineMock = new();
    private readonly Mock<ICuriosityEngine> _curiosityEngineMock = new();
    private readonly Mock<IExternalKnowledgeSource> _knowledgeSourceMock = new();

    // ── GenerateHypothesisFromResearchAsync ────────────────────────

    [Fact]
    public async Task GenerateHypothesisFromResearchAsync_WhenSearchFails_ReturnsFailure()
    {
        // Arrange
        _knowledgeSourceMock
            .Setup(k => k.SearchPapersAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ResearchPaper>, string>.Failure("API error"));

        // Act
        var result = await _hypothesisEngineMock.Object
            .GenerateHypothesisFromResearchAsync(_knowledgeSourceMock.Object, "test topic");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to fetch research");
    }

    [Fact]
    public async Task GenerateHypothesisFromResearchAsync_WhenNoObservations_ReturnsFailure()
    {
        // Arrange
        var papers = new List<ResearchPaper>
        {
            new("1", "Paper", "Author", "Abstract", "cs.AI", "url")
        };
        _knowledgeSourceMock
            .Setup(k => k.SearchPapersAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ResearchPaper>, string>.Success(papers));
        _knowledgeSourceMock
            .Setup(k => k.ExtractObservationsAsync(It.IsAny<List<ResearchPaper>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        // Act
        var result = await _hypothesisEngineMock.Object
            .GenerateHypothesisFromResearchAsync(_knowledgeSourceMock.Object, "test topic");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No observations");
    }

    [Fact]
    public async Task GenerateHypothesisFromResearchAsync_WithObservations_CallsAbductiveReasoning()
    {
        // Arrange
        var papers = new List<ResearchPaper>
        {
            new("1", "Paper", "Author", "Abstract", "cs.AI", "url")
        };
        var observations = new List<string> { "Observation 1", "Observation 2" };
        var hypothesis = new Hypothesis(
            Guid.NewGuid(), "Generated hypothesis", "cs.AI", 0.6,
            new List<string>(), new List<string>(),
            DateTime.UtcNow, false, null);

        _knowledgeSourceMock
            .Setup(k => k.SearchPapersAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<ResearchPaper>, string>.Success(papers));
        _knowledgeSourceMock
            .Setup(k => k.ExtractObservationsAsync(It.IsAny<List<ResearchPaper>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(observations);
        _hypothesisEngineMock
            .Setup(h => h.AbductiveReasoningAsync(observations, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Hypothesis, string>.Success(hypothesis));

        // Act
        var result = await _hypothesisEngineMock.Object
            .GenerateHypothesisFromResearchAsync(_knowledgeSourceMock.Object, "test topic");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(hypothesis);
        _hypothesisEngineMock.Verify(
            h => h.AbductiveReasoningAsync(observations, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── EnrichWithResearchOpportunitiesAsync ───────────────────────

    [Fact]
    public async Task EnrichWithResearchOpportunitiesAsync_MergesAndOrdersOpportunities()
    {
        // Arrange
        var researchOpps = new List<ExplorationOpportunity>
        {
            new("Research opp 1", 0.9, 0.8, new List<string>(), DateTime.UtcNow),
            new("Research opp 2", 0.5, 0.5, new List<string>(), DateTime.UtcNow)
        };
        var curiosityOpps = new List<ExplorationOpportunity>
        {
            new("Curiosity opp 1", 0.7, 0.9, new List<string>(), DateTime.UtcNow)
        };

        _knowledgeSourceMock
            .Setup(k => k.IdentifyResearchOpportunitiesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(researchOpps);
        _curiosityEngineMock
            .Setup(c => c.IdentifyExplorationOpportunitiesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(curiosityOpps);

        // Act
        var result = await _curiosityEngineMock.Object
            .EnrichWithResearchOpportunitiesAsync(_knowledgeSourceMock.Object, "test domain");

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task EnrichWithResearchOpportunitiesAsync_LimitsToTen()
    {
        // Arrange
        var manyOpps = Enumerable.Range(0, 8)
            .Select(i => new ExplorationOpportunity($"Opp {i}", 0.5, 0.5, new List<string>(), DateTime.UtcNow))
            .ToList();

        _knowledgeSourceMock
            .Setup(k => k.IdentifyResearchOpportunitiesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(manyOpps);
        _curiosityEngineMock
            .Setup(c => c.IdentifyExplorationOpportunitiesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(manyOpps);

        // Act
        var result = await _curiosityEngineMock.Object
            .EnrichWithResearchOpportunitiesAsync(_knowledgeSourceMock.Object, "domain");

        // Assert
        result.Should().HaveCountLessThanOrEqualTo(10);
    }

    [Fact]
    public async Task EnrichWithResearchOpportunitiesAsync_OrdersByWeightedScore()
    {
        // Arrange
        var researchOpps = new List<ExplorationOpportunity>
        {
            new("Low score", 0.1, 0.1, new List<string>(), DateTime.UtcNow),
            new("High score", 1.0, 1.0, new List<string>(), DateTime.UtcNow)
        };

        _knowledgeSourceMock
            .Setup(k => k.IdentifyResearchOpportunitiesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(researchOpps);
        _curiosityEngineMock
            .Setup(c => c.IdentifyExplorationOpportunitiesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExplorationOpportunity>());

        // Act
        var result = await _curiosityEngineMock.Object
            .EnrichWithResearchOpportunitiesAsync(_knowledgeSourceMock.Object, "domain");

        // Assert
        result.First().Description.Should().Be("High score");
    }
}
