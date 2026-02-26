using Ouroboros.Providers;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ConsciousnessEventTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var ts = DateTime.UtcNow;
        var evt = new ConsciousnessEvent(ConsciousnessEventType.Emergence, "Emerged!", ts);

        evt.Type.Should().Be(ConsciousnessEventType.Emergence);
        evt.Message.Should().Be("Emerged!");
        evt.Timestamp.Should().Be(ts);
    }
}
