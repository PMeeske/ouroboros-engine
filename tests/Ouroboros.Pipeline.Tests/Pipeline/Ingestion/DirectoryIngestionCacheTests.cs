namespace Ouroboros.Tests.Pipeline.Ingestion;

using Ouroboros.Pipeline.Ingestion;

[Trait("Category", "Unit")]
public class DirectoryIngestionCacheTests
{
    [Fact]
    public void Constructor_WithNonExistentPath_CreatesEmptyCache()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"cache-{Guid.NewGuid()}.json");

        // Act
        var cache = new DirectoryIngestionCache(path);

        // Assert — no exception should be thrown
        cache.Should().NotBeNull();
    }

    [Fact]
    public void IsUnchanged_ForUnknownFile_ReturnsFalse()
    {
        // Arrange
        var cachePath = Path.Combine(Path.GetTempPath(), $"cache-{Guid.NewGuid()}.json");
        var cache = new DirectoryIngestionCache(cachePath);

        // Act
        var result = cache.IsUnchanged("nonexistent-file.txt");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void UpdateHash_ThenIsUnchanged_ReturnsTrue()
    {
        // Arrange
        var cachePath = Path.Combine(Path.GetTempPath(), $"cache-{Guid.NewGuid()}.json");
        var filePath = Path.Combine(Path.GetTempPath(), $"testfile-{Guid.NewGuid()}.txt");
        try
        {
            File.WriteAllText(filePath, "hello world");
            var cache = new DirectoryIngestionCache(cachePath);

            // Act
            cache.UpdateHash(filePath);
            var result = cache.IsUnchanged(filePath);

            // Assert
            result.Should().BeTrue();
        }
        finally
        {
            File.Delete(filePath);
            File.Delete(cachePath);
        }
    }

    [Fact]
    public void IsUnchanged_AfterFileModified_ReturnsFalse()
    {
        // Arrange
        var cachePath = Path.Combine(Path.GetTempPath(), $"cache-{Guid.NewGuid()}.json");
        var filePath = Path.Combine(Path.GetTempPath(), $"testfile-{Guid.NewGuid()}.txt");
        try
        {
            File.WriteAllText(filePath, "original content");
            var cache = new DirectoryIngestionCache(cachePath);
            cache.UpdateHash(filePath);

            // Act
            File.WriteAllText(filePath, "modified content");
            var result = cache.IsUnchanged(filePath);

            // Assert
            result.Should().BeFalse();
        }
        finally
        {
            File.Delete(filePath);
            File.Delete(cachePath);
        }
    }

    [Fact]
    public void Persist_SavesCacheToDisk()
    {
        // Arrange
        var cachePath = Path.Combine(Path.GetTempPath(), $"cache-{Guid.NewGuid()}.json");
        var filePath = Path.Combine(Path.GetTempPath(), $"testfile-{Guid.NewGuid()}.txt");
        try
        {
            File.WriteAllText(filePath, "content to hash");
            var cache = new DirectoryIngestionCache(cachePath);
            cache.UpdateHash(filePath);

            // Act
            cache.Persist();

            // Assert
            File.Exists(cachePath).Should().BeTrue();
            var json = File.ReadAllText(cachePath);
            json.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            File.Delete(filePath);
            File.Delete(cachePath);
        }
    }

    [Fact]
    public void Persist_WithNoChanges_DoesNotWriteFile()
    {
        // Arrange
        var cachePath = Path.Combine(Path.GetTempPath(), $"cache-{Guid.NewGuid()}.json");
        var cache = new DirectoryIngestionCache(cachePath);

        // Act
        cache.Persist();

        // Assert
        File.Exists(cachePath).Should().BeFalse();
    }

    [Fact]
    public void Constructor_LoadsExistingCacheFromDisk()
    {
        // Arrange
        var cachePath = Path.Combine(Path.GetTempPath(), $"cache-{Guid.NewGuid()}.json");
        var filePath = Path.Combine(Path.GetTempPath(), $"testfile-{Guid.NewGuid()}.txt");
        try
        {
            File.WriteAllText(filePath, "persisted content");

            // First cache instance: update and persist
            var cache1 = new DirectoryIngestionCache(cachePath);
            cache1.UpdateHash(filePath);
            cache1.Persist();

            // Act — second cache instance should load from disk
            var cache2 = new DirectoryIngestionCache(cachePath);
            var result = cache2.IsUnchanged(filePath);

            // Assert
            result.Should().BeTrue();
        }
        finally
        {
            File.Delete(filePath);
            File.Delete(cachePath);
        }
    }

    [Fact]
    public void Constructor_WithInvalidJson_DoesNotThrow()
    {
        // Arrange
        var cachePath = Path.Combine(Path.GetTempPath(), $"cache-{Guid.NewGuid()}.json");
        try
        {
            File.WriteAllText(cachePath, "{ invalid json content !!!");

            // Act
            var cache = new DirectoryIngestionCache(cachePath);

            // Assert — should silently ignore corrupt cache
            cache.Should().NotBeNull();
        }
        finally
        {
            File.Delete(cachePath);
        }
    }

    [Fact]
    public void IsUnchanged_WhenFileDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var cachePath = Path.Combine(Path.GetTempPath(), $"cache-{Guid.NewGuid()}.json");
        var cache = new DirectoryIngestionCache(cachePath);

        // Act
        var result = cache.IsUnchanged(Path.Combine(Path.GetTempPath(), "does-not-exist.txt"));

        // Assert
        result.Should().BeFalse();
    }
}
