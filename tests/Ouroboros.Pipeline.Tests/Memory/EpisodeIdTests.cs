namespace Ouroboros.Tests.Pipeline.Memory;

/// <summary>
/// Unit tests for EpisodeId record type.
/// </summary>
[Trait("Category", "Unit")]
public class EpisodeIdTests
{
    [Fact]
    public void EpisodeId_ShouldStoreGuid()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var episodeId = new EpisodeId(guid);

        // Assert
        episodeId.Value.Should().Be(guid);
    }

    [Fact]
    public void EpisodeId_Equality_ShouldWorkCorrectly()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new EpisodeId(guid);
        var id2 = new EpisodeId(guid);
        var id3 = new EpisodeId(Guid.NewGuid());

        // Assert
        id1.Should().Be(id2); // Same GUID
        id1.Should().NotBe(id3); // Different GUID
    }
}