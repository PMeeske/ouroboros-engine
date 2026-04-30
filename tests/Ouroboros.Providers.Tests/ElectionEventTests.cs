namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ElectionEventTests
{
    [Fact]
    public void Ctor_SetsProperties()
    {
        var ts = DateTime.UtcNow;
        var evt = new ElectionEvent(ElectionEventType.ElectionStarted, "Starting", ts);

        evt.Type.Should().Be(ElectionEventType.ElectionStarted);
        evt.Message.Should().Be("Starting");
        evt.Timestamp.Should().Be(ts);
        evt.Winner.Should().BeNull();
        evt.Votes.Should().BeNull();
    }

    [Fact]
    public void Ctor_WithOptionalParams_SetsAll()
    {
        var votes = new Dictionary<string, double> { ["a"] = 0.8 };
        var evt = new ElectionEvent(
            ElectionEventType.ElectionComplete,
            "Done",
            DateTime.UtcNow,
            Winner: "a",
            Votes: votes);

        evt.Winner.Should().Be("a");
        evt.Votes.Should().ContainKey("a");
    }
}
