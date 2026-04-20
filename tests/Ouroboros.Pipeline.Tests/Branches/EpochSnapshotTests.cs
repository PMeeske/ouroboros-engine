using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Pipeline.Branches;

[Trait("Category", "Unit")]
public sealed class EpochSnapshotTests
{
    [Fact]
    public void Constructor_WithRequiredProperties_SetsValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var branches = new List<BranchSnapshot>();

        // Act
        var snapshot = new EpochSnapshot
        {
            EpochId = id,
            EpochNumber = 42,
            CreatedAt = now,
            Branches = branches
        };

        // Assert
        snapshot.EpochId.Should().Be(id);
        snapshot.EpochNumber.Should().Be(42);
        snapshot.CreatedAt.Should().Be(now);
        snapshot.Branches.Should().BeSameAs(branches);
    }

    [Fact]
    public void Metadata_DefaultsToEmptyDictionary()
    {
        // Act
        var snapshot = new EpochSnapshot
        {
            EpochId = Guid.NewGuid(),
            EpochNumber = 1,
            CreatedAt = DateTime.UtcNow,
            Branches = new List<BranchSnapshot>()
        };

        // Assert
        snapshot.Metadata.Should().NotBeNull();
        snapshot.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void Metadata_CanBeSetToCustomDictionary()
    {
        // Act
        var metadata = new Dictionary<string, object>
        {
            ["key1"] = "value1",
            ["key2"] = 42
        };

        var snapshot = new EpochSnapshot
        {
            EpochId = Guid.NewGuid(),
            EpochNumber = 1,
            CreatedAt = DateTime.UtcNow,
            Branches = new List<BranchSnapshot>(),
            Metadata = metadata
        };

        // Assert
        snapshot.Metadata.Should().HaveCount(2);
        snapshot.Metadata["key1"].Should().Be("value1");
    }

    [Fact]
    public void Hash_DefaultsToNull()
    {
        // Act
        var snapshot = new EpochSnapshot
        {
            EpochId = Guid.NewGuid(),
            EpochNumber = 1,
            CreatedAt = DateTime.UtcNow,
            Branches = new List<BranchSnapshot>()
        };

        // Assert
        snapshot.Hash.Should().BeNull();
    }

    [Fact]
    public void Hash_CanBeSet()
    {
        // Act
        var snapshot = new EpochSnapshot
        {
            EpochId = Guid.NewGuid(),
            EpochNumber = 1,
            CreatedAt = DateTime.UtcNow,
            Branches = new List<BranchSnapshot>(),
            Hash = "abc123"
        };

        // Assert
        snapshot.Hash.Should().Be("abc123");
    }

    [Fact]
    public void Branches_ContainsProvidedSnapshots()
    {
        // Arrange
        var branch1 = new BranchSnapshot { Name = "branch-1" };
        var branch2 = new BranchSnapshot { Name = "branch-2" };

        // Act
        var snapshot = new EpochSnapshot
        {
            EpochId = Guid.NewGuid(),
            EpochNumber = 1,
            CreatedAt = DateTime.UtcNow,
            Branches = new List<BranchSnapshot> { branch1, branch2 }
        };

        // Assert
        snapshot.Branches.Should().HaveCount(2);
        snapshot.Branches[0].Name.Should().Be("branch-1");
        snapshot.Branches[1].Name.Should().Be("branch-2");
    }
}
