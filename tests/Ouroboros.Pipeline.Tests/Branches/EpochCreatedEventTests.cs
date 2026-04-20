using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Pipeline.Branches;

[Trait("Category", "Unit")]
public sealed class EpochCreatedEventTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var epoch = CreateEpochSnapshot();
        var timestamp = DateTime.UtcNow;

        // Act
        var evt = new EpochCreatedEvent(id, epoch, timestamp);

        // Assert
        evt.Id.Should().Be(id);
        evt.Epoch.Should().BeSameAs(epoch);
        evt.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void FromEpoch_CreatesEventWithNewGuid()
    {
        // Arrange
        var epoch = CreateEpochSnapshot();

        // Act
        var evt = EpochCreatedEvent.FromEpoch(epoch);

        // Assert
        evt.Id.Should().NotBe(Guid.Empty);
        evt.Epoch.Should().BeSameAs(epoch);
        evt.Timestamp.Should().Be(epoch.CreatedAt);
    }

    [Fact]
    public void FromEpoch_MultipleCalls_GenerateDifferentIds()
    {
        // Arrange
        var epoch = CreateEpochSnapshot();

        // Act
        var evt1 = EpochCreatedEvent.FromEpoch(epoch);
        var evt2 = EpochCreatedEvent.FromEpoch(epoch);

        // Assert
        evt1.Id.Should().NotBe(evt2.Id);
    }

    [Fact]
    public void FromEpoch_UsesEpochTimestamp()
    {
        // Arrange
        var specificTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var epoch = new EpochSnapshot
        {
            EpochId = Guid.NewGuid(),
            EpochNumber = 1,
            CreatedAt = specificTime,
            Branches = new List<BranchSnapshot>()
        };

        // Act
        var evt = EpochCreatedEvent.FromEpoch(epoch);

        // Assert
        evt.Timestamp.Should().Be(specificTime);
    }

    private static EpochSnapshot CreateEpochSnapshot()
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
