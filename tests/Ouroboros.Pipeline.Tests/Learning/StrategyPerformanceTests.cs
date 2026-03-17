using Ouroboros.Pipeline.Learning;

namespace Ouroboros.Tests.Learning;

public class StrategyPerformanceTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var strategyId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var hyperparams = ImmutableDictionary<string, double>.Empty
            .Add("lr", 0.01)
            .Add("epsilon", 0.1);

        // Act
        var perf = new StrategyPerformance(strategyId, 0.85, timestamp, hyperparams);

        // Assert
        perf.StrategyId.Should().Be(strategyId);
        perf.Score.Should().Be(0.85);
        perf.Timestamp.Should().Be(timestamp);
        perf.Hyperparameters.Should().HaveCount(2);
        perf.Hyperparameters["lr"].Should().Be(0.01);
        perf.Hyperparameters["epsilon"].Should().Be(0.1);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var hyperparams = ImmutableDictionary<string, double>.Empty;

        // Act
        var perf1 = new StrategyPerformance(id, 0.5, timestamp, hyperparams);
        var perf2 = new StrategyPerformance(id, 0.5, timestamp, hyperparams);

        // Assert
        perf1.Should().Be(perf2);
    }

    [Fact]
    public void RecordEquality_WithDifferentScores_AreNotEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var hyperparams = ImmutableDictionary<string, double>.Empty;

        // Act
        var perf1 = new StrategyPerformance(id, 0.5, timestamp, hyperparams);
        var perf2 = new StrategyPerformance(id, 0.9, timestamp, hyperparams);

        // Assert
        perf1.Should().NotBe(perf2);
    }
}
