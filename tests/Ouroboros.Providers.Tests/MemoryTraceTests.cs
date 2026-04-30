namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class MemoryTraceTests
{
    [Fact]
    public void Ctor_SetsProperties()
    {
        var ts = DateTime.UtcNow;
        var trace = new MemoryTrace("pathway1", "content", "thinking", ts, 0.8);

        trace.Pathway.Should().Be("pathway1");
        trace.Content.Should().Be("content");
        trace.Thinking.Should().Be("thinking");
        trace.Timestamp.Should().Be(ts);
        trace.Salience.Should().Be(0.8);
    }

    [Fact]
    public void Ctor_NullThinking_IsAllowed()
    {
        var trace = new MemoryTrace("p", "c", null, DateTime.UtcNow, 0.5);
        trace.Thinking.Should().BeNull();
    }
}
