using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Pipeline.Branches;

[Trait("Category", "Unit")]
public sealed class SnapshotMetadataTests
{
    [Fact]
    public void Constructor_WithRequiredProperties_SetsValues()
    {
        // Arrange
        var createdAt = DateTime.UtcNow;

        // Act
        var metadata = new SnapshotMetadata
        {
            Id = "snap-1",
            BranchName = "main",
            CreatedAt = createdAt,
            Hash = "abc123def456"
        };

        // Assert
        metadata.Id.Should().Be("snap-1");
        metadata.BranchName.Should().Be("main");
        metadata.CreatedAt.Should().Be(createdAt);
        metadata.Hash.Should().Be("abc123def456");
    }

    [Fact]
    public void SizeBytes_DefaultsToZero()
    {
        // Act
        var metadata = new SnapshotMetadata
        {
            Id = "snap",
            BranchName = "branch",
            CreatedAt = DateTime.UtcNow,
            Hash = "hash"
        };

        // Assert
        metadata.SizeBytes.Should().Be(0);
    }

    [Fact]
    public void SizeBytes_CanBeSet()
    {
        // Act
        var metadata = new SnapshotMetadata
        {
            Id = "snap",
            BranchName = "branch",
            CreatedAt = DateTime.UtcNow,
            Hash = "hash",
            SizeBytes = 1024 * 1024 // 1 MB
        };

        // Assert
        metadata.SizeBytes.Should().Be(1024 * 1024);
    }

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        // Arrange
        var time = DateTime.UtcNow;
        var meta1 = new SnapshotMetadata { Id = "id", BranchName = "b", CreatedAt = time, Hash = "h" };
        var meta2 = new SnapshotMetadata { Id = "id", BranchName = "b", CreatedAt = time, Hash = "h" };

        // Assert
        meta1.Should().Be(meta2);
    }

    [Fact]
    public void Equality_WithDifferentId_AreNotEqual()
    {
        // Arrange
        var time = DateTime.UtcNow;
        var meta1 = new SnapshotMetadata { Id = "id-1", BranchName = "b", CreatedAt = time, Hash = "h" };
        var meta2 = new SnapshotMetadata { Id = "id-2", BranchName = "b", CreatedAt = time, Hash = "h" };

        // Assert
        meta1.Should().NotBe(meta2);
    }
}
