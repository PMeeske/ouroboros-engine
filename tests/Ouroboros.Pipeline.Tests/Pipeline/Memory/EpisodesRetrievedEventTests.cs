namespace Ouroboros.Tests.Pipeline.Memory;

[Trait("Category", "Unit")]
public class EpisodesRetrievedEventTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsProperties()
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
    }

    [Fact]
    public void Constructor_WithAutoId_GeneratesNewGuid()
    {
        // Arrange
        var count = 3;
        var query = "search term";
        var timestamp = DateTime.UtcNow;

        // Act
        var evt = new EpisodesRetrievedEvent(count, query, timestamp);

        // Assert
        evt.Id.Should().NotBe(Guid.Empty);
        evt.Count.Should().Be(count);
        evt.Query.Should().Be(query);
        evt.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void Constructor_WithAutoId_GeneratesUniqueIds()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var evt1 = new EpisodesRetrievedEvent(1, "q1", timestamp);
        var evt2 = new EpisodesRetrievedEvent(2, "q2", timestamp);

        // Assert
        evt1.Id.Should().NotBe(evt2.Id);
    }

    [Fact]
    public void Constructor_ZeroCount_SetsProperty()
    {
        // Act
        var evt = new EpisodesRetrievedEvent(0, "empty query", DateTime.UtcNow);

        // Assert
        evt.Count.Should().Be(0);
    }

    [Fact]
    public void Equality_IdenticalValues_AreEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var ts = DateTime.UtcNow;
        var a = new EpisodesRetrievedEvent(id, 5, "query", ts);
        var b = new EpisodesRetrievedEvent(id, 5, "query", ts);

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentCount_AreNotEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var ts = DateTime.UtcNow;
        var a = new EpisodesRetrievedEvent(id, 5, "query", ts);
        var b = new EpisodesRetrievedEvent(id, 10, "query", ts);

        // Act & Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        // Arrange
        var original = new EpisodesRetrievedEvent(3, "original", DateTime.UtcNow);

        // Act
        var modified = original with { Count = 7 };

        // Assert
        modified.Count.Should().Be(7);
        modified.Query.Should().Be("original");
        original.Count.Should().Be(3);
    }
}
