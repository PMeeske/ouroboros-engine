namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class GlobalNetworkStateDeltaTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var now = DateTimeOffset.UtcNow;
        var delta = new GlobalNetworkStateDelta(1, 5, 10, 8, now);

        delta.FromEpoch.Should().Be(1);
        delta.ToEpoch.Should().Be(5);
        delta.NodeDelta.Should().Be(10);
        delta.TransitionDelta.Should().Be(8);
        delta.Timestamp.Should().Be(now);
    }
}
