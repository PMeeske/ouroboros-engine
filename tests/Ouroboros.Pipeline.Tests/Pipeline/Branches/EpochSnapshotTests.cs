namespace Ouroboros.Tests.Pipeline.Branches;

using Ouroboros.Pipeline.Branches;

[Trait("Category", "Unit")]
public class EpochSnapshotTests
{
    [Fact]
    public void Constructor_SetsRequiredProperties()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var branches = new List<BranchSnapshot>();

        var epoch = new EpochSnapshot
        {
            EpochId = id,
            EpochNumber = 42,
            CreatedAt = now,
            Branches = branches
        };

        epoch.EpochId.Should().Be(id);
        epoch.EpochNumber.Should().Be(42);
        epoch.CreatedAt.Should().Be(now);
        epoch.Branches.Should().BeSameAs(branches);
    }

    [Fact]
    public void Metadata_DefaultsToEmptyDictionary()
    {
        var epoch = new EpochSnapshot
        {
            EpochId = Guid.NewGuid(),
            EpochNumber = 1,
            CreatedAt = DateTime.UtcNow,
            Branches = new List<BranchSnapshot>()
        };

        epoch.Metadata.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Hash_DefaultsToNull()
    {
        var epoch = new EpochSnapshot
        {
            EpochId = Guid.NewGuid(),
            EpochNumber = 1,
            CreatedAt = DateTime.UtcNow,
            Branches = new List<BranchSnapshot>()
        };

        epoch.Hash.Should().BeNull();
    }

    [Fact]
    public void Hash_CanBeSetViaInitializer()
    {
        var epoch = new EpochSnapshot
        {
            EpochId = Guid.NewGuid(),
            EpochNumber = 1,
            CreatedAt = DateTime.UtcNow,
            Branches = new List<BranchSnapshot>(),
            Hash = "abc123"
        };

        epoch.Hash.Should().Be("abc123");
    }
}
