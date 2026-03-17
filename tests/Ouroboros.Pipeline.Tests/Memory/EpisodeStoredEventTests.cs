using FluentAssertions;

namespace Ouroboros.Tests.Memory;

[Trait("Category", "Unit")]
public class EpisodeStoredEventTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var episodeId = new EpisodeId(Guid.NewGuid());
        var goal = "test goal";
        var success = true;
        var timestamp = DateTime.UtcNow;

        // Act
        var evt = new EpisodeStoredEvent(id, episodeId, goal, success, timestamp);

        // Assert
        evt.Id.Should().Be(id);
        evt.EpisodeId.Should().Be(episodeId);
        evt.Goal.Should().Be(goal);
        evt.Success.Should().BeTrue();
        evt.Timestamp.Should().Be(timestamp);
        evt.EventType.Should().Be("EpisodeStored");
    }

    [Fact]
    public void ConvenienceConstructor_GeneratesNewId()
    {
        // Arrange
        var episodeId = new EpisodeId(Guid.NewGuid());
        var timestamp = DateTime.UtcNow;

        // Act
        var evt = new EpisodeStoredEvent(episodeId, "goal", true, timestamp);

        // Assert
        evt.Id.Should().NotBe(Guid.Empty);
        evt.EpisodeId.Should().Be(episodeId);
        evt.Goal.Should().Be("goal");
        evt.Success.Should().BeTrue();
        evt.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void ConvenienceConstructor_WithFailedSuccess_SetsSuccessToFalse()
    {
        // Arrange
        var episodeId = new EpisodeId(Guid.NewGuid());

        // Act
        var evt = new EpisodeStoredEvent(episodeId, "failed goal", false, DateTime.UtcNow);

        // Assert
        evt.Success.Should().BeFalse();
    }

    [Fact]
    public void ConvenienceConstructor_TwoInvocations_GenerateDifferentIds()
    {
        // Arrange
        var episodeId = new EpisodeId(Guid.NewGuid());
        var timestamp = DateTime.UtcNow;

        // Act
        var evt1 = new EpisodeStoredEvent(episodeId, "goal", true, timestamp);
        var evt2 = new EpisodeStoredEvent(episodeId, "goal", true, timestamp);

        // Assert
        evt1.Id.Should().NotBe(evt2.Id);
    }

    [Fact]
    public void EventType_IsEpisodeStored()
    {
        // Arrange
        var episodeId = new EpisodeId(Guid.NewGuid());

        // Act
        var evt = new EpisodeStoredEvent(episodeId, "goal", true, DateTime.UtcNow);

        // Assert
        evt.EventType.Should().Be("EpisodeStored");
    }

    [Fact]
    public void InheritsFromPipelineEvent()
    {
        // Arrange
        var episodeId = new EpisodeId(Guid.NewGuid());

        // Act
        var evt = new EpisodeStoredEvent(episodeId, "goal", true, DateTime.UtcNow);

        // Assert
        evt.Should().BeAssignableTo<PipelineEvent>();
    }
}
