namespace Ouroboros.Tests.Pipeline.Branches;

using Ouroboros.Pipeline.Branches;

[Trait("Category", "Unit")]
public class BranchPersistenceTests
{
    [Fact]
    public async Task SaveAsync_AndLoadAsync_RoundTripsSnapshot()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"branch-{Guid.NewGuid()}.json");
        try
        {
            var snapshot = new BranchSnapshot { Name = "test-branch" };

            // Act
            await BranchPersistence.SaveAsync(snapshot, path);
            var loaded = await BranchPersistence.LoadAsync(path);

            // Assert
            loaded.Should().NotBeNull();
            loaded.Name.Should().Be("test-branch");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAsync_CreatesFile()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"branch-{Guid.NewGuid()}.json");
        try
        {
            var snapshot = new BranchSnapshot { Name = "file-check" };

            // Act
            await BranchPersistence.SaveAsync(snapshot, path);

            // Assert
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAsync_WritesValidJson()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"branch-{Guid.NewGuid()}.json");
        try
        {
            var snapshot = new BranchSnapshot { Name = "json-check" };

            // Act
            await BranchPersistence.SaveAsync(snapshot, path);
            var json = await File.ReadAllTextAsync(path);

            // Assert
            json.Should().Contain("json-check");
            json.Should().Contain("Name");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAsync_WritesIndentedJson()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"branch-{Guid.NewGuid()}.json");
        try
        {
            var snapshot = new BranchSnapshot { Name = "indent-check" };

            // Act
            await BranchPersistence.SaveAsync(snapshot, path);
            var json = await File.ReadAllTextAsync(path);

            // Assert
            json.Should().Contain("\n");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_WithEmptyEvents_ReturnsSnapshotWithEmptyList()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"branch-{Guid.NewGuid()}.json");
        try
        {
            var snapshot = new BranchSnapshot { Name = "empty-events" };
            await BranchPersistence.SaveAsync(snapshot, path);

            // Act
            var loaded = await BranchPersistence.LoadAsync(path);

            // Assert
            loaded.Events.Should().BeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveAsync_OverwritesExistingFile()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"branch-{Guid.NewGuid()}.json");
        try
        {
            var snapshot1 = new BranchSnapshot { Name = "first" };
            var snapshot2 = new BranchSnapshot { Name = "second" };

            // Act
            await BranchPersistence.SaveAsync(snapshot1, path);
            await BranchPersistence.SaveAsync(snapshot2, path);
            var loaded = await BranchPersistence.LoadAsync(path);

            // Assert
            loaded.Name.Should().Be("second");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
