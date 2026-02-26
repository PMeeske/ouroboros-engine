namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ElectionResultTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var winner = ResponseCandidate<string>.Create("best answer", "gpt-4", TimeSpan.FromMilliseconds(200));
        var all = new List<ResponseCandidate<string>>
        {
            winner,
            ResponseCandidate<string>.Create("ok answer", "claude", TimeSpan.FromMilliseconds(300)),
        };
        var votes = new Dictionary<string, double> { ["gpt-4"] = 0.8, ["claude"] = 0.5 };

        var result = new ElectionResult<string>(
            Winner: winner,
            AllCandidates: all,
            Strategy: ElectionStrategy.WeightedMajority,
            Rationale: "highest score",
            Votes: votes);

        result.Winner.Should().BeSameAs(winner);
        result.AllCandidates.Should().HaveCount(2);
        result.Strategy.Should().Be(ElectionStrategy.WeightedMajority);
        result.Rationale.Should().Be("highest score");
        result.Votes.Should().HaveCount(2);
        result.Votes["gpt-4"].Should().Be(0.8);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var winner = ResponseCandidate<string>.Create("answer", "src", TimeSpan.Zero);
        var candidates = new List<ResponseCandidate<string>> { winner };
        var votes = new Dictionary<string, double> { ["src"] = 1.0 };

        var result1 = new ElectionResult<string>(winner, candidates, ElectionStrategy.Majority, "reason", votes);
        var result2 = new ElectionResult<string>(winner, candidates, ElectionStrategy.Majority, "reason", votes);

        result1.Should().Be(result2);
    }
}
