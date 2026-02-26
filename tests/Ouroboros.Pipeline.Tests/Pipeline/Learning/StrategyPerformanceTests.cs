namespace Ouroboros.Tests.Pipeline.Learning;

using Ouroboros.Pipeline.Learning;

[Trait("Category", "Unit")]
public class StrategyPerformanceTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var hyperparams = ImmutableDictionary<string, double>.Empty
            .Add("learningRate", 0.01);

        var perf = new StrategyPerformance(id, 0.85, timestamp, hyperparams);

        perf.StrategyId.Should().Be(id);
        perf.Score.Should().Be(0.85);
        perf.Timestamp.Should().Be(timestamp);
        perf.Hyperparameters.Should().ContainKey("learningRate");
    }

    [Fact]
    public void Record_SupportsEquality()
    {
        var id = Guid.NewGuid();
        var ts = DateTime.UtcNow;
        var hp = ImmutableDictionary<string, double>.Empty;

        var p1 = new StrategyPerformance(id, 0.5, ts, hp);
        var p2 = new StrategyPerformance(id, 0.5, ts, hp);

        p1.Should().Be(p2);
    }
}
