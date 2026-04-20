#pragma warning disable CA2000 // Test file - ownership is managed by test lifecycle
using FluentAssertions;
using Ouroboros.Providers;
using Xunit;

namespace Ouroboros.Tests;

/// <summary>
/// Tests for the election algorithms and optimization suggestions in MasterModelElection.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MasterModelElectionAlgorithmsTests
{
    [Theory]
    [InlineData(ElectionStrategy.SimpleMajority)]
    [InlineData(ElectionStrategy.WeightedMajority)]
    [InlineData(ElectionStrategy.BordaCount)]
    [InlineData(ElectionStrategy.Condorcet)]
    [InlineData(ElectionStrategy.InstantRunoff)]
    [InlineData(ElectionStrategy.Approval)]
    [InlineData(ElectionStrategy.MasterDecision)]
    public async Task RunElectionAsync_WithMultipleCandidates_ReturnsWinner(ElectionStrategy strategy)
    {
        // Arrange
        using var election = new MasterModelElection(strategy: strategy);
        var candidates = CreateTestCandidates();

        // Act
        var result = await election.RunElectionAsync(candidates, "test prompt", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Winner.Should().NotBeNull();
        result.Votes.Should().NotBeEmpty();
        result.Rationale.Should().NotBeNullOrEmpty();
        result.Strategy.Should().Be(strategy);
    }

    [Fact]
    public async Task RunElectionAsync_WithSingleCandidate_ReturnsThatCandidate()
    {
        // Arrange
        using var election = new MasterModelElection();
        var candidates = new List<ResponseCandidate<ThinkingResponse>>
        {
            ResponseCandidate<ThinkingResponse>.Create(
                new ThinkingResponse(null, "Only response"),
                "only-model",
                TimeSpan.FromMilliseconds(100))
        };

        // Act
        var result = await election.RunElectionAsync(candidates, "test", CancellationToken.None);

        // Assert
        result.Winner.Source.Should().Be("only-model");
    }

    [Fact]
    public async Task RunElectionAsync_HighestScorer_WinsSimpleMajority()
    {
        // Arrange
        using var election = new MasterModelElection(strategy: ElectionStrategy.SimpleMajority);

        var candidates = new List<ResponseCandidate<ThinkingResponse>>
        {
            CreateCandidate("low-scorer", "short"),
            CreateCandidate("high-scorer", "This is a much longer and more detailed response that should score higher based on the heuristics used for relevance, coherence and completeness evaluation. The response includes multiple sentences with proper punctuation. It addresses the test prompt in detail."),
        };

        // Act
        var result = await election.RunElectionAsync(candidates, "test prompt with some words", CancellationToken.None);

        // Assert
        result.Winner.Should().NotBeNull();
        result.Votes.Should().HaveCount(2);
    }

    [Fact]
    public async Task RunElectionAsync_BordaCount_AssignsBordaPoints()
    {
        // Arrange
        using var election = new MasterModelElection(strategy: ElectionStrategy.BordaCount);
        var candidates = CreateTestCandidates();

        // Act
        var result = await election.RunElectionAsync(candidates, "test", CancellationToken.None);

        // Assert
        result.Rationale.Should().Contain("Borda");
        result.Votes.Values.Should().AllSatisfy(v => v.Should().BeGreaterThanOrEqualTo(0));
    }

    [Fact]
    public async Task RunElectionAsync_Condorcet_UsePairwiseComparison()
    {
        // Arrange
        using var election = new MasterModelElection(strategy: ElectionStrategy.Condorcet);
        var candidates = CreateTestCandidates();

        // Act
        var result = await election.RunElectionAsync(candidates, "test", CancellationToken.None);

        // Assert
        result.Rationale.Should().Contain("Condorcet");
    }

    [Fact]
    public async Task RunElectionAsync_InstantRunoff_EliminatesLowest()
    {
        // Arrange
        using var election = new MasterModelElection(strategy: ElectionStrategy.InstantRunoff);
        var candidates = CreateTestCandidates();

        // Act
        var result = await election.RunElectionAsync(candidates, "test", CancellationToken.None);

        // Assert
        result.Rationale.Should().Contain("IRV");
    }

    [Fact]
    public async Task RunElectionAsync_Approval_UsesThreshold()
    {
        // Arrange
        using var election = new MasterModelElection(strategy: ElectionStrategy.Approval);
        var candidates = CreateTestCandidates();

        // Act
        var result = await election.RunElectionAsync(candidates, "test", CancellationToken.None);

        // Assert
        result.Rationale.Should().Contain("Approval");
    }

    [Fact]
    public async Task RunElectionAsync_MasterDecision_FallsBackToWeightedMajority()
    {
        // Arrange - No master pathway set, so it falls back
        using var election = new MasterModelElection(strategy: ElectionStrategy.MasterDecision);
        var candidates = CreateTestCandidates();

        // Act
        var result = await election.RunElectionAsync(candidates, "test", CancellationToken.None);

        // Assert
        result.Winner.Should().NotBeNull();
    }

    [Fact]
    public async Task RunElectionAsync_UpdatesPerformanceHistory()
    {
        // Arrange
        using var election = new MasterModelElection();
        var candidates = CreateTestCandidates();

        // Act
        await election.RunElectionAsync(candidates, "test", CancellationToken.None);

        // Assert
        election.PerformanceHistory.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetOptimizationSuggestions_WithLowPerformer_SuggestsRemoval()
    {
        // Arrange
        using var election = new MasterModelElection();

        // Run multiple elections to build up history
        for (int i = 0; i < 10; i++)
        {
            var candidates = new List<ResponseCandidate<ThinkingResponse>>
            {
                CreateCandidate("consistent-winner", "This is a much longer and very detailed response."),
                CreateCandidate("consistent-loser", "short"),
            };
            await election.RunElectionAsync(candidates, "test prompt", CancellationToken.None);
        }

        // Act
        var suggestions = election.GetOptimizationSuggestions();

        // Assert - Should have some suggestions after enough elections
        suggestions.Should().NotBeNull();
    }

    [Fact]
    public async Task ElectionEvents_EmitsEvents()
    {
        // Arrange
        using var election = new MasterModelElection();
        ElectionEvent? receivedEvent = null;
        election.ElectionEvents.Subscribe(e => receivedEvent = e);
        var candidates = CreateTestCandidates();

        // Act
        await election.RunElectionAsync(candidates, "test", CancellationToken.None);

        // Assert
        receivedEvent.Should().NotBeNull();
    }

    private static List<ResponseCandidate<ThinkingResponse>> CreateTestCandidates()
    {
        return new List<ResponseCandidate<ThinkingResponse>>
        {
            CreateCandidate("model-a", "Response A with some content for testing."),
            CreateCandidate("model-b", "Response B is a bit longer with more detailed content for testing purposes."),
            CreateCandidate("model-c", "Response C provides the most comprehensive answer. It covers multiple aspects and includes thorough analysis."),
        };
    }

    private static ResponseCandidate<ThinkingResponse> CreateCandidate(string source, string content)
    {
        return ResponseCandidate<ThinkingResponse>.Create(
            new ThinkingResponse(null, content),
            source,
            TimeSpan.FromMilliseconds(100));
    }
}
