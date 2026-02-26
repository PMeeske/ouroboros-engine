namespace Ouroboros.Tests.Persistence;

[Trait("Category", "Unit")]
public sealed class WalEntryTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var ts = DateTimeOffset.UtcNow;
        var entry = new WalEntry(WalEntryType.AddNode, ts, "{\"id\":\"1\"}");

        entry.Type.Should().Be(WalEntryType.AddNode);
        entry.Timestamp.Should().Be(ts);
        entry.PayloadJson.Should().Contain("1");
    }
}
