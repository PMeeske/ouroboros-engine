using FluentAssertions;
using Ouroboros.Network;
using Xunit;

namespace Ouroboros.Tests.Network;

[Trait("Category", "Unit")]
public class FeedbackLoopConfigTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var config = new FeedbackLoopConfig();

        config.DivergenceThreshold.Should().Be(0.5f);
        config.RotationThreshold.Should().Be(0.3f);
        config.MaxModificationsPerCycle.Should().Be(10);
        config.AutoPersist.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithCustomValues_SetsProperties()
    {
        var config = new FeedbackLoopConfig(0.7f, 0.4f, 20, false);

        config.DivergenceThreshold.Should().Be(0.7f);
        config.RotationThreshold.Should().Be(0.4f);
        config.MaxModificationsPerCycle.Should().Be(20);
        config.AutoPersist.Should().BeFalse();
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        var a = new FeedbackLoopConfig(0.5f, 0.3f, 10, true);
        var b = new FeedbackLoopConfig(0.5f, 0.3f, 10, true);

        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class FeedbackResultTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var duration = TimeSpan.FromMilliseconds(500);

        var result = new FeedbackResult(100, 15, 5, 3, 7, duration);

        result.NodesAnalyzed.Should().Be(100);
        result.NodesModified.Should().Be(15);
        result.SourceNodes.Should().Be(5);
        result.SinkNodes.Should().Be(3);
        result.CyclicNodes.Should().Be(7);
        result.Duration.Should().Be(duration);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        var d = TimeSpan.FromSeconds(1);
        var a = new FeedbackResult(10, 2, 1, 1, 0, d);
        var b = new FeedbackResult(10, 2, 1, 1, 0, d);

        a.Should().Be(b);
    }
}
