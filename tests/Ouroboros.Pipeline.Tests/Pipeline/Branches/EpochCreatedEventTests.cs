namespace Ouroboros.Tests.Pipeline.Branches;

using Ouroboros.Pipeline.Branches;

[Trait("Category", "Unit")]
public class EpochCreatedEventTests
{
    [Fact]
    public void FromEpoch_CreatesEventWithCorrectProperties()
    {
        var epoch = CreateTestEpoch();
        var evt = EpochCreatedEvent.FromEpoch(epoch);

        evt.Id.Should().NotBe(Guid.Empty);
        evt.Epoch.Should().BeSameAs(epoch);
        evt.Timestamp.Should().Be(epoch.CreatedAt);
    }

    [Fact]
    public void Constructor_SetsBaseProperties()
    {
        var id = Guid.NewGuid();
        var epoch = CreateTestEpoch();
        var ts = DateTime.UtcNow;

        var evt = new EpochCreatedEvent(id, epoch, ts);

        evt.Id.Should().Be(id);
        evt.Epoch.Should().BeSameAs(epoch);
        evt.Timestamp.Should().Be(ts);
    }

    private static EpochSnapshot CreateTestEpoch()
    {
        return new EpochSnapshot
        {
            EpochId = Guid.NewGuid(),
            EpochNumber = 1,
            CreatedAt = DateTime.UtcNow,
            Branches = new List<BranchSnapshot>()
        };
    }
}
