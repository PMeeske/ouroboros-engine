using System.IO.Compression;
using FluentAssertions;
using Ouroboros.Pipeline.Ingestion.Zip;

namespace Ouroboros.Tests.Zip;

[Trait("Category", "Unit")]
public class ZipArchiveHolderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _tempFiles = new();

    public ZipArchiveHolderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ZipHolderTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
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
    public void Constructor_ValidPath_OpensArchive()
    {
        // Arrange
        var path = CreateTestZip(("test.txt", "hello"));

        // Act
        using var holder = new ZipArchiveHolder(path);

        // Assert
        holder.Archive.Should().NotBeNull();
        holder.Stream.Should().NotBeNull();
        holder.Archive.Entries.Should().HaveCount(1);
    }

    [Fact]
    public void Constructor_InvalidPath_ThrowsFileNotFoundException()
    {
        // Arrange & Act
        var act = () => new ZipArchiveHolder("/nonexistent/path/file.zip");

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void AddRef_IncrementsReferenceCount()
    {
        // Arrange
        var path = CreateTestZip(("test.txt", "content"));
        using var holder = new ZipArchiveHolder(path);

        // Act
        holder.AddRef();
        var count = holder.ReleaseRef();

        // Assert - after AddRef (count=2) then ReleaseRef (count=1)
        count.Should().Be(1);
    }

    [Fact]
    public void ReleaseRef_DecrementsReferenceCount()
    {
        // Arrange
        var path = CreateTestZip(("test.txt", "content"));
        using var holder = new ZipArchiveHolder(path);

        // Act
        var count = holder.ReleaseRef();

        // Assert - initial count is 1, after release is 0
        count.Should().Be(0);
    }

    [Fact]
    public void ReleaseRef_MultipleCalls_ContinuesDecrementing()
    {
        // Arrange
        var path = CreateTestZip(("test.txt", "content"));
        using var holder = new ZipArchiveHolder(path);
        holder.AddRef();
        holder.AddRef();

        // Act
        var count1 = holder.ReleaseRef(); // 3 -> 2
        var count2 = holder.ReleaseRef(); // 2 -> 1
        var count3 = holder.ReleaseRef(); // 1 -> 0

        // Assert
        count1.Should().Be(2);
        count2.Should().Be(1);
        count3.Should().Be(0);
    }

    [Fact]
    public void Dispose_ClosesArchiveAndStream()
    {
        // Arrange
        var path = CreateTestZip(("test.txt", "content"));
        var holder = new ZipArchiveHolder(path);

        // Act
        holder.Dispose();

        // Assert - stream should be closed
        holder.Stream.CanRead.Should().BeFalse();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var path = CreateTestZip(("test.txt", "content"));
        var holder = new ZipArchiveHolder(path);

        // Act
        var act = () =>
        {
            holder.Dispose();
            holder.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }
}
