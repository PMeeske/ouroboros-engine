using FluentAssertions;

namespace Ouroboros.Tests.Memory;

[Trait("Category", "Unit")]
public class EpisodeIdTests
{
    [Fact]
    public void Constructor_WithGuid_SetsValue()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var episodeId = new EpisodeId(guid);

        // Assert
        episodeId.Value.Should().Be(guid);
    }

    [Fact]
    public void Constructor_WithEmptyGuid_SetsEmptyValue()
    {
        // Act
        var episodeId = new EpisodeId(Guid.Empty);

        // Assert
        episodeId.Value.Should().Be(Guid.Empty);
    }

    [Fact]
    public void RecordEquality_WithSameGuid_AreEqual()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new EpisodeId(guid);
        var id2 = new EpisodeId(guid);

        // Assert
        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
    }

    [Fact]
    public void RecordEquality_WithDifferentGuids_AreNotEqual()
    {
        // Arrange
        var id1 = new EpisodeId(Guid.NewGuid());
        var id2 = new EpisodeId(Guid.NewGuid());

        // Assert
        id1.Should().NotBe(id2);
        (id1 != id2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_WithSameGuid_ReturnsSameHash()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new EpisodeId(guid);
        var id2 = new EpisodeId(guid);

        // Assert
        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void ToString_ContainsGuidValue()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var episodeId = new EpisodeId(guid);

        // Act
        var str = episodeId.ToString();

        // Assert
        str.Should().Contain(guid.ToString());
    }
}
