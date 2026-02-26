namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class MasterModelElectionTests
{
    [Fact]
    public void Ctor_Defaults_SetsStrategy()
    {
        using var election = new MasterModelElection();

        election.Strategy.Should().Be(ElectionStrategy.WeightedMajority);
        election.PerformanceHistory.Should().BeEmpty();
    }

    [Fact]
    public void Ctor_CustomStrategy_IsPreserved()
    {
        using var election = new MasterModelElection(strategy: ElectionStrategy.BordaCount);

        election.Strategy.Should().Be(ElectionStrategy.BordaCount);
    }

    [Fact]
    public void Strategy_CanBeChanged()
    {
        using var election = new MasterModelElection();

        election.Strategy = ElectionStrategy.InstantRunoff;

        election.Strategy.Should().Be(ElectionStrategy.InstantRunoff);
    }

    [Fact]
    public void GetOptimizationSuggestions_EmptyHistory_ReturnsEmpty()
    {
        using var election = new MasterModelElection();

        var suggestions = election.GetOptimizationSuggestions();

        suggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task RunElectionAsync_NoCandidates_Throws()
    {
        using var election = new MasterModelElection();
        var empty = new List<ResponseCandidate<ThinkingResponse>>();

        await FluentActions.Invoking(() => election.RunElectionAsync(empty, "test", CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var election = new MasterModelElection();

        FluentActions.Invoking(() => election.Dispose()).Should().NotThrow();
    }

    [Fact]
    public void ElectionEvents_IsObservable()
    {
        using var election = new MasterModelElection();

        election.ElectionEvents.Should().NotBeNull();
    }
}
