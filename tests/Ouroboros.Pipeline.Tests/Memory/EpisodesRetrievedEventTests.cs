using FluentAssertions;

namespace Ouroboros.Tests.Memory;

[Trait("Category", "Unit")]
public class EpisodesRetrievedEventTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var count = 5;
        var query = "test query";
        var timestamp = DateTime.UtcNow;

        // Act
        var evt = new EpisodesRetrievedEvent(id, count, query, timestamp);

        // Assert
        evt.Id.Should().Be(id);
        evt.Count.Should().Be(count);
        evt.Query.Should().Be(query);
        evt.Timestamp.Should().Be(timestamp);
        evt.EventType.Should().Be("EpisodesRetrieved");
    }

    [Fact]
    public void ConvenienceConstructor_GeneratesNewId()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var evt = new EpisodesRetrievedEvent(3, "search query", timestamp);

        // Assert
        evt.Id.Should().NotBe(Guid.Empty);
        evt.Count.Should().Be(3);
        evt.Query.Should().Be("search query");
        evt.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void ConvenienceConstructor_TwoInvocations_GenerateDifferentIds()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var evt1 = new EpisodesRetrievedEvent(1, "query", timestamp);
        var evt2 = new EpisodesRetrievedEvent(1, "query", timestamp);

        // Assert
        evt1.Id.Should().NotBe(evt2.Id);
    }

    [Fact]
    public void Count_WithZero_IsValid()
    {
        // Act
        var evt = new EpisodesRetrievedEvent(0, "empty query", DateTime.UtcNow);

        // Assert
        evt.Count.Should().Be(0);
    }

    [Fact]
    public void EventType_IsEpisodesRetrieved()
    {
        // Act
        var evt = new EpisodesRetrievedEvent(1, "query", DateTime.UtcNow);

        // Assert
        evt.EventType.Should().Be("EpisodesRetrieved");
    }

    [Fact]
    public void InheritsFromPipelineEvent()
    {
        // Act
        var evt = new EpisodesRetrievedEvent(1, "query", DateTime.UtcNow);

        // Assert
        evt.Should().BeAssignableTo<PipelineEvent>();
    }
}
