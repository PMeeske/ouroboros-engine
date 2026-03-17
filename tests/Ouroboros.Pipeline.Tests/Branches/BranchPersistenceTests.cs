using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Pipeline.Branches;

[Trait("Category", "Unit")]
public sealed class BranchPersistenceTests : IDisposable
{
    private readonly string _testDir;

    public BranchPersistenceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"branch-persistence-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    [Fact]
    public async Task SaveAsync_CreatesJsonFile()
    {
        // Arrange
        var snapshot = new BranchSnapshot
        {
            Name = "test-branch",
            Events = new List<PipelineEvent>(),
            Vectors = new List<SerializableVector>()
        };
        string path = Path.Combine(_testDir, "snapshot.json");

        // Act
        await BranchPersistence.SaveAsync(snapshot, path);

        // Assert
        File.Exists(path).Should().BeTrue();
        string content = await File.ReadAllTextAsync(path);
        content.Should().Contain("test-branch");
    }

    [Fact]
    public async Task LoadAsync_ReturnsDeserializedSnapshot()
    {
        // Arrange
        var snapshot = new BranchSnapshot
        {
            Name = "loaded-branch",
            Events = new List<PipelineEvent>(),
            Vectors = new List<SerializableVector>
            {
                new SerializableVector
                {
                    Id = "v1",
                    Text = "content",
                    Embedding = new[] { 1.0f, 2.0f },
                    Metadata = new Dictionary<string, object>()
                }
            }
        };
        string path = Path.Combine(_testDir, "load-test.json");
        await BranchPersistence.SaveAsync(snapshot, path);

        // Act
        var loaded = await BranchPersistence.LoadAsync(path);

        // Assert
        loaded.Should().NotBeNull();
        loaded.Name.Should().Be("loaded-branch");
        loaded.Vectors.Should().HaveCount(1);
        loaded.Vectors[0].Id.Should().Be("v1");
        loaded.Vectors[0].Text.Should().Be("content");
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesData()
    {
        // Arrange
        var original = new BranchSnapshot
        {
            Name = "roundtrip-branch",
            Events = new List<PipelineEvent>(),
            Vectors = new List<SerializableVector>
            {
                new SerializableVector
                {
                    Id = "vec-1",
                    Text = "Test content",
                    Embedding = new[] { 0.5f, 1.5f, 2.5f },
                    Metadata = new Dictionary<string, object> { ["key"] = "value" }
                }
            }
        };
        string path = Path.Combine(_testDir, "roundtrip.json");

        // Act
        await BranchPersistence.SaveAsync(original, path);
        var loaded = await BranchPersistence.LoadAsync(path);

        // Assert
        loaded.Name.Should().Be(original.Name);
        loaded.Vectors.Should().HaveCount(original.Vectors.Count);
        loaded.Vectors[0].Id.Should().Be(original.Vectors[0].Id);
        loaded.Vectors[0].Text.Should().Be(original.Vectors[0].Text);
    }

    [Fact]
    public async Task SaveAsync_WithEmptySnapshot_CreatesValidJson()
    {
        // Arrange
        var snapshot = new BranchSnapshot
        {
            Name = "",
            Events = new List<PipelineEvent>(),
            Vectors = new List<SerializableVector>()
        };
        string path = Path.Combine(_testDir, "empty.json");

        // Act
        await BranchPersistence.SaveAsync(snapshot, path);

        // Assert
        File.Exists(path).Should().BeTrue();
        string content = await File.ReadAllTextAsync(path);
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SaveAsync_OverwritesExistingFile()
    {
        // Arrange
        string path = Path.Combine(_testDir, "overwrite.json");
        var snapshot1 = new BranchSnapshot { Name = "first" };
        var snapshot2 = new BranchSnapshot { Name = "second" };

        // Act
        await BranchPersistence.SaveAsync(snapshot1, path);
        await BranchPersistence.SaveAsync(snapshot2, path);

        // Assert
        var loaded = await BranchPersistence.LoadAsync(path);
        loaded.Name.Should().Be("second");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
