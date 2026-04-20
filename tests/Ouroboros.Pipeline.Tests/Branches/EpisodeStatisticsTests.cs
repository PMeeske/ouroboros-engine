using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Pipeline.Branches;

[Trait("Category", "Unit")]
public sealed class EpisodeStatisticsTests
{
    [Fact]
    public void Constructor_WithValidValues_SetsAllProperties()
    {
        // Act
        var stats = new EpisodeStatistics(
            TotalEpisodes: 10,
            SuccessfulEpisodes: 7,
            SuccessRate: 0.7,
            AverageReward: 15.5,
            AverageSteps: 42.3,
            TotalReward: 155.0);

        // Assert
        stats.TotalEpisodes.Should().Be(10);
        stats.SuccessfulEpisodes.Should().Be(7);
        stats.SuccessRate.Should().BeApproximately(0.7, 0.001);
        stats.AverageReward.Should().BeApproximately(15.5, 0.001);
        stats.AverageSteps.Should().BeApproximately(42.3, 0.001);
        stats.TotalReward.Should().BeApproximately(155.0, 0.001);
    }

    [Fact]
    public void Constructor_WithZeroValues_SetsAllToZero()
    {
        // Act
        var stats = new EpisodeStatistics(0, 0, 0.0, 0.0, 0.0, 0.0);

        // Assert
        stats.TotalEpisodes.Should().Be(0);
        stats.SuccessfulEpisodes.Should().Be(0);
        stats.SuccessRate.Should().Be(0.0);
        stats.AverageReward.Should().Be(0.0);
        stats.AverageSteps.Should().Be(0.0);
        stats.TotalReward.Should().Be(0.0);
    }

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        // Arrange
        var stats1 = new EpisodeStatistics(5, 3, 0.6, 10.0, 20.0, 50.0);
        var stats2 = new EpisodeStatistics(5, 3, 0.6, 10.0, 20.0, 50.0);

        // Assert
        stats1.Should().Be(stats2);
    }

    [Fact]
    public void Equality_WithDifferentValues_AreNotEqual()
    {
        // Arrange
        var stats1 = new EpisodeStatistics(5, 3, 0.6, 10.0, 20.0, 50.0);
        var stats2 = new EpisodeStatistics(5, 4, 0.8, 10.0, 20.0, 50.0);

        // Assert
        stats1.Should().NotBe(stats2);
    }
}
