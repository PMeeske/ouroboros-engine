using System.IO.Compression;
using FluentAssertions;
using Ouroboros.Pipeline.Ingestion.Zip;

namespace Ouroboros.Tests.Zip;

[Trait("Category", "Unit")]
public class ZipArchiveRegistryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _tempFiles = new();

    public ZipArchiveRegistryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ZipRegTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        // Release any held archives to prevent file locking
        foreach (var file in _tempFiles)
        {
            try { ZipArchiveRegistry.Release(file); } catch { /* best effort */ }
            try { File.Delete(file); } catch { /* best effort */ }
        }
        try { Directory.Delete(_tempDir, true); } catch { /* best effort */ }
    }

    private string CreateTestZip(params (string name, string content)[] entries)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".zip");
        using (var fs = File.Create(path))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
        }
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public void Acquire_ValidPath_ReturnsHolder()
    {
        // Arrange
        var path = CreateTestZip(("test.txt", "hello"));

        // Act
        var holder = ZipArchiveRegistry.Acquire(path);

        // Assert
        holder.Should().NotBeNull();
        holder.Archive.Should().NotBeNull();

        // Cleanup
        ZipArchiveRegistry.Release(path);
    }

    [Fact]
    public void Acquire_SamePathTwice_ReturnsSameHolder()
    {
        // Arrange
        var path = CreateTestZip(("test.txt", "content"));

        // Act
        var holder1 = ZipArchiveRegistry.Acquire(path);
        var holder2 = ZipArchiveRegistry.Acquire(path);

        // Assert
        holder1.Should().BeSameAs(holder2);

        // Cleanup - release twice since acquired twice
        ZipArchiveRegistry.Release(path);
        ZipArchiveRegistry.Release(path);
    }

    [Fact]
    public void Release_LastReference_RemovesFromRegistry()
    {
        // Arrange
        var path = CreateTestZip(("test.txt", "content"));
        ZipArchiveRegistry.Acquire(path);

        // Act
        ZipArchiveRegistry.Release(path);

        // Assert - acquiring again should create a new holder
        var newHolder = ZipArchiveRegistry.Acquire(path);
        newHolder.Should().NotBeNull();

        // Cleanup
        ZipArchiveRegistry.Release(path);
    }

    [Fact]
    public void Release_NonExistentPath_DoesNotThrow()
    {
        // Arrange & Act
        var act = () => ZipArchiveRegistry.Release("/nonexistent/path.zip");

        // Assert
        act.Should().NotThrow();
    }
}
