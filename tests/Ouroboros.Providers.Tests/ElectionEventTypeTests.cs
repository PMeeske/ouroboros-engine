using Ouroboros.Providers;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ElectionEventTypeTests
{
    [Fact]
    public void Enum_HasSixMembers()
    {
        Enum.GetValues<ElectionEventType>().Should().HaveCount(6);
    }
}
