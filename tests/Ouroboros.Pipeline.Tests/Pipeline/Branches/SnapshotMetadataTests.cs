namespace Ouroboros.Tests.Pipeline.Branches;

using Ouroboros.Pipeline.Branches;

[Trait("Category", "Unit")]
public class SnapshotMetadataTests
{
    [Fact]
    public void Constructor_SetsAllRequiredProperties()
    {
        var now = DateTime.UtcNow;
        var meta = new SnapshotMetadata
        {
            Id = "snap-1",
            BranchName = "branch-a",
            CreatedAt = now,
            Hash = "abc123"
        };

        meta.Id.Should().Be("snap-1");
        meta.BranchName.Should().Be("branch-a");
        meta.CreatedAt.Should().Be(now);
        meta.Hash.Should().Be("abc123");
    }

    [Fact]
    public void SizeBytes_DefaultsToZero()
    {
        var meta = new SnapshotMetadata
        {
            Id = "snap-1",
            BranchName = "branch-a",
            CreatedAt = DateTime.UtcNow,
            Hash = "abc"
        };

        meta.SizeBytes.Should().Be(0);
    }

    [Fact]
    public void SizeBytes_CanBeSet()
    {
        var meta = new SnapshotMetadata
        {
            Id = "snap-1",
            BranchName = "branch-a",
            CreatedAt = DateTime.UtcNow,
            Hash = "abc",
            SizeBytes = 1024
        };

        meta.SizeBytes.Should().Be(1024);
    }
}
