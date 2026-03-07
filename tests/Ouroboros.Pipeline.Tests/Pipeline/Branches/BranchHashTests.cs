namespace Ouroboros.Tests.Pipeline.Branches;

using Ouroboros.Pipeline.Branches;

[Trait("Category", "Unit")]
public class BranchHashTests
{
    [Fact]
    public void ComputeHash_WithValidSnapshot_ReturnsDeterministicHash()
    {
        var snapshot = CreateTestSnapshot();
        var hash1 = BranchHash.ComputeHash(snapshot);
        var hash2 = BranchHash.ComputeHash(snapshot);
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeHash_WithNullSnapshot_ThrowsArgumentNullException()
    {
        var act = () => BranchHash.ComputeHash(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ComputeHash_ReturnsLowercaseHexString()
    {
        var snapshot = CreateTestSnapshot();
        var hash = BranchHash.ComputeHash(snapshot);
        hash.Should().MatchRegex("^[0-9a-f]+$");
        hash.Should().HaveLength(64); // SHA-256 = 32 bytes = 64 hex chars
    }

    [Fact]
    public void VerifyHash_WithMatchingHash_ReturnsTrue()
    {
        var snapshot = CreateTestSnapshot();
        var hash = BranchHash.ComputeHash(snapshot);
        BranchHash.VerifyHash(snapshot, hash).Should().BeTrue();
    }

    [Fact]
    public void VerifyHash_WithNonMatchingHash_ReturnsFalse()
    {
        var snapshot = CreateTestSnapshot();
        BranchHash.VerifyHash(snapshot, "invalidhash").Should().BeFalse();
    }

    [Fact]
    public void VerifyHash_IsCaseInsensitive()
    {
        var snapshot = CreateTestSnapshot();
        var hash = BranchHash.ComputeHash(snapshot);
        BranchHash.VerifyHash(snapshot, hash.ToUpperInvariant()).Should().BeTrue();
    }

    [Fact]
    public void VerifyHash_WithNullSnapshot_ThrowsArgumentNullException()
    {
        var act = () => BranchHash.VerifyHash(null!, "hash");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void VerifyHash_WithNullHash_ThrowsArgumentNullException()
    {
        var act = () => BranchHash.VerifyHash(CreateTestSnapshot(), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithHash_ReturnsTupleContainingSnapshotAndHash()
    {
        var snapshot = CreateTestSnapshot();
        var (resultSnapshot, hash) = BranchHash.WithHash(snapshot);
        resultSnapshot.Should().BeSameAs(snapshot);
        hash.Should().NotBeNullOrEmpty();
        BranchHash.VerifyHash(resultSnapshot, hash).Should().BeTrue();
    }

    [Fact]
    public void WithHash_WithNullSnapshot_ThrowsArgumentNullException()
    {
        var act = () => BranchHash.WithHash(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    private static BranchSnapshot CreateTestSnapshot()
    {
        return new BranchSnapshot
        {
            Name = "test-branch",
            Events = [],
            Vectors = []
        };
    }
}
