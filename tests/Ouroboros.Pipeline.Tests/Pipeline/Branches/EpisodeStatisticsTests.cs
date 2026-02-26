namespace Ouroboros.Tests.Pipeline.Branches;

using Ouroboros.Pipeline.Branches;

[Trait("Category", "Unit")]
public class EpisodeStatisticsTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var stats = new EpisodeStatistics(
            TotalEpisodes: 10,
            SuccessfulEpisodes: 7,
            SuccessRate: 0.7,
            AverageReward: 0.85,
            AverageSteps: 12.5,
            TotalReward: 8.5);

        stats.TotalEpisodes.Should().Be(10);
        stats.SuccessfulEpisodes.Should().Be(7);
        stats.SuccessRate.Should().Be(0.7);
        stats.AverageReward.Should().Be(0.85);
        stats.AverageSteps.Should().Be(12.5);
        stats.TotalReward.Should().Be(8.5);
    }

    [Fact]
    public void Record_SupportsWithExpression()
    {
        var stats = new EpisodeStatistics(10, 7, 0.7, 0.85, 12.5, 8.5);
        var updated = stats with { TotalEpisodes = 20 };
        updated.TotalEpisodes.Should().Be(20);
        stats.TotalEpisodes.Should().Be(10);
    }

    [Fact]
    public void Record_SupportsEqualityComparison()
    {
        var stats1 = new EpisodeStatistics(10, 7, 0.7, 0.85, 12.5, 8.5);
        var stats2 = new EpisodeStatistics(10, 7, 0.7, 0.85, 12.5, 8.5);
        stats1.Should().Be(stats2);
    }
}
