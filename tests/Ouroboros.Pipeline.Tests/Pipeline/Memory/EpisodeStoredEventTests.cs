namespace Ouroboros.Tests.Pipeline.Memory;

[Trait("Category", "Unit")]
public class EpisodeStoredEventTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsProperties()
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
    }

    [Fact]
    public void Constructor_WithAutoId_GeneratesNewGuid()
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
    public void Constructor_WithAutoId_GeneratesUniqueIds()
    {
        // Arrange
        var episodeId = new EpisodeId(Guid.NewGuid());
        var timestamp = DateTime.UtcNow;

        // Act
        var evt1 = new EpisodeStoredEvent(episodeId, "goal1", true, timestamp);
        var evt2 = new EpisodeStoredEvent(episodeId, "goal2", false, timestamp);

        // Assert
        evt1.Id.Should().NotBe(evt2.Id);
    }

    [Fact]
    public void Constructor_FailureCase_SetsSuccessFalse()
    {
        // Act
        var evt = new EpisodeStoredEvent(
            new EpisodeId(Guid.NewGuid()), "failed goal", false, DateTime.UtcNow);

        // Assert
        evt.Success.Should().BeFalse();
    }

    [Fact]
    public void Equality_IdenticalValues_AreEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var episodeId = new EpisodeId(Guid.NewGuid());
        var ts = DateTime.UtcNow;
        var a = new EpisodeStoredEvent(id, episodeId, "goal", true, ts);
        var b = new EpisodeStoredEvent(id, episodeId, "goal", true, ts);

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentGoal_AreNotEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var episodeId = new EpisodeId(Guid.NewGuid());
        var ts = DateTime.UtcNow;
        var a = new EpisodeStoredEvent(id, episodeId, "goal1", true, ts);
        var b = new EpisodeStoredEvent(id, episodeId, "goal2", true, ts);

        // Act & Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        // Arrange
        var original = new EpisodeStoredEvent(
            new EpisodeId(Guid.NewGuid()), "original", true, DateTime.UtcNow);

        // Act
        var modified = original with { Goal = "modified" };

        // Assert
        modified.Goal.Should().Be("modified");
        modified.Success.Should().BeTrue();
        original.Goal.Should().Be("original");
    }
}
