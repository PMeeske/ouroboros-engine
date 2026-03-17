using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Pipeline.Branches;

[Trait("Category", "Unit")]
public sealed class BranchHashTests
{
    private static BranchSnapshot CreateSnapshot(string name = "test", int eventCount = 0, int vectorCount = 0)
    {
        var snapshot = new BranchSnapshot
        {
            Name = name,
            Events = new List<PipelineEvent>(),
            Vectors = new List<SerializableVector>()
        };

        for (int i = 0; i < eventCount; i++)
        {
            snapshot.Events.Add(new IngestBatch(
                Guid.NewGuid(),
                $"source-{i}",
                new List<string> { $"doc-{i}" },
                DateTime.UtcNow));
        }

        for (int i = 0; i < vectorCount; i++)
        {
            snapshot.Vectors.Add(new SerializableVector
            {
                Id = $"vec-{i}",
                Text = $"Content {i}",
                Embedding = new[] { (float)i, (float)(i + 1) },
                Metadata = new Dictionary<string, object>()
            });
        }

        return snapshot;
    }

    #region ComputeHash Tests

    [Fact]
    public void ComputeHash_WithValidSnapshot_ReturnsHexString()
    {
        // Arrange
        var snapshot = CreateSnapshot("test-branch");

        // Act
        string hash = BranchHash.ComputeHash(snapshot);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Should().MatchRegex("^[0-9a-f]{64}$"); // SHA-256 hex
    }

    [Fact]
    public void ComputeHash_WithNullSnapshot_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => BranchHash.ComputeHash(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ComputeHash_SameSnapshot_ReturnsSameHash()
    {
        // Arrange
        var snapshot = CreateSnapshot("deterministic", vectorCount: 2);

        // Act
        string hash1 = BranchHash.ComputeHash(snapshot);
        string hash2 = BranchHash.ComputeHash(snapshot);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeHash_DifferentNames_ReturnsDifferentHashes()
    {
        // Arrange
        var snapshot1 = CreateSnapshot("branch-a");
        var snapshot2 = CreateSnapshot("branch-b");

        // Act
        string hash1 = BranchHash.ComputeHash(snapshot1);
        string hash2 = BranchHash.ComputeHash(snapshot2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeHash_DifferentVectors_ReturnsDifferentHashes()
    {
        // Arrange
        var snapshot1 = CreateSnapshot("test", vectorCount: 1);
        var snapshot2 = CreateSnapshot("test", vectorCount: 2);

        // Act
        string hash1 = BranchHash.ComputeHash(snapshot1);
        string hash2 = BranchHash.ComputeHash(snapshot2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeHash_ReturnsLowercaseHex()
    {
        // Arrange
        var snapshot = CreateSnapshot();

        // Act
        string hash = BranchHash.ComputeHash(snapshot);

        // Assert
        hash.Should().Be(hash.ToLowerInvariant());
    }

    [Fact]
    public void ComputeHash_WithEmptySnapshot_ReturnsValidHash()
    {
        // Arrange
        var snapshot = new BranchSnapshot
        {
            Name = "",
            Events = new List<PipelineEvent>(),
            Vectors = new List<SerializableVector>()
        };

        // Act
        string hash = BranchHash.ComputeHash(snapshot);

        // Assert
        hash.Should().HaveLength(64);
    }

    #endregion

    #region VerifyHash Tests

    [Fact]
    public void VerifyHash_WithMatchingHash_ReturnsTrue()
    {
        // Arrange
        var snapshot = CreateSnapshot("verify-test", vectorCount: 1);
        string expected = BranchHash.ComputeHash(snapshot);

        // Act
        bool result = BranchHash.VerifyHash(snapshot, expected);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyHash_WithMismatchingHash_ReturnsFalse()
    {
        // Arrange
        var snapshot = CreateSnapshot("verify-test");

        // Act
        bool result = BranchHash.VerifyHash(snapshot, "0000000000000000000000000000000000000000000000000000000000000000");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyHash_IsCaseInsensitive()
    {
        // Arrange
        var snapshot = CreateSnapshot("case-test");
        string hash = BranchHash.ComputeHash(snapshot);

        // Act
        bool result = BranchHash.VerifyHash(snapshot, hash.ToUpperInvariant());

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyHash_WithNullSnapshot_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => BranchHash.VerifyHash(null!, "hash");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void VerifyHash_WithNullHash_ThrowsArgumentNullException()
    {
        // Arrange
        var snapshot = CreateSnapshot();

        // Act
        Action act = () => BranchHash.VerifyHash(snapshot, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region WithHash Tests

    [Fact]
    public void WithHash_ReturnsSnapshotAndHash()
    {
        // Arrange
        var snapshot = CreateSnapshot("hash-test", vectorCount: 1);

        // Act
        var (returnedSnapshot, hash) = BranchHash.WithHash(snapshot);

        // Assert
        returnedSnapshot.Should().BeSameAs(snapshot);
        hash.Should().NotBeNullOrEmpty();
        hash.Should().HaveLength(64);
    }

    [Fact]
    public void WithHash_HashMatchesComputeHash()
    {
        // Arrange
        var snapshot = CreateSnapshot("consistency-test");

        // Act
        var (_, hash) = BranchHash.WithHash(snapshot);
        string directHash = BranchHash.ComputeHash(snapshot);

        // Assert
        hash.Should().Be(directHash);
    }

    [Fact]
    public void WithHash_WithNullSnapshot_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => BranchHash.WithHash(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion
}
