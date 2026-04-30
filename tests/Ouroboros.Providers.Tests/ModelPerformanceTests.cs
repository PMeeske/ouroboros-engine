namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ModelPerformanceTests
{
    [Fact]
    public void WinRate_ZeroElections_ReturnsZero()
    {
        var perf = new ModelPerformance { TotalElections = 0, Wins = 0 };
        perf.WinRate.Should().Be(0);
    }

    [Fact]
    public void WinRate_AllWins_ReturnsOne()
    {
        var perf = new ModelPerformance { TotalElections = 10, Wins = 10 };
        perf.WinRate.Should().Be(1.0);
    }

    [Fact]
    public void WinRate_HalfWins_ReturnsHalf()
    {
        var perf = new ModelPerformance { TotalElections = 100, Wins = 50 };
        perf.WinRate.Should().Be(0.5);
    }

    [Fact]
    public void ReliabilityScore_HighWinRateLowLatency_IsHigh()
    {
        var perf = new ModelPerformance
        {
            TotalElections = 100,
            Wins = 90,
            AverageLatency = TimeSpan.FromSeconds(1)
        };

        perf.ReliabilityScore.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void ReliabilityScore_ZeroElections_IsNonNegative()
    {
        var perf = new ModelPerformance { TotalElections = 0, Wins = 0 };
        perf.ReliabilityScore.Should().BeGreaterThanOrEqualTo(0);
    }
}
