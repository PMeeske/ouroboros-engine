using FluentAssertions;
using Ouroboros.Pipeline.Ingestion.Zip;

namespace Ouroboros.Tests.Zip;

[Trait("Category", "Unit")]
public class ZipFileRecordTests
{
    private static Func<Stream> DummyOpener => () => new MemoryStream();

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange & Act
        var record = new ZipFileRecord(
            FullPath: "dir/file.txt",
            Directory: "dir",
            FileName: "file.txt",
            Kind: ZipContentKind.Text,
            Length: 1024,
            CompressedLength: 512,
            CompressionRatio: 2.0,
            OpenStream: DummyOpener,
            Parsed: null,
            ZipPath: "/tmp/archive.zip",
            MaxCompressionRatioLimit: 200.0);

        // Assert
        record.FullPath.Should().Be("dir/file.txt");
        record.Directory.Should().Be("dir");
        record.FileName.Should().Be("file.txt");
        record.Kind.Should().Be(ZipContentKind.Text);
        record.Length.Should().Be(1024);
        record.CompressedLength.Should().Be(512);
        record.CompressionRatio.Should().Be(2.0);
        record.OpenStream.Should().NotBeNull();
        record.Parsed.Should().BeNull();
        record.ZipPath.Should().Be("/tmp/archive.zip");
        record.MaxCompressionRatioLimit.Should().Be(200.0);
    }

    [Fact]
    public void Constructor_WithNullDirectory_SetsToNull()
    {
        // Arrange & Act
        var record = new ZipFileRecord("file.csv", null, "file.csv",
            ZipContentKind.Csv, 100, 50, 2.0, DummyOpener, null, "/tmp/a.zip", 200);

        // Assert
        record.Directory.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithParsedDictionary_SetsParsed()
    {
        // Arrange
        var parsed = new Dictionary<string, object> { ["type"] = "csv", ["rows"] = 10 };

        // Act
        var record = new ZipFileRecord("data.csv", null, "data.csv",
            ZipContentKind.Csv, 500, 200, 2.5, DummyOpener, parsed, "/tmp/a.zip", 200);

        // Assert
        record.Parsed.Should().NotBeNull();
        record.Parsed!["type"].Should().Be("csv");
        record.Parsed["rows"].Should().Be(10);
    }

    [Fact]
    public void OpenStream_ReturnsStream()
    {
        // Arrange
        var record = new ZipFileRecord("f.txt", null, "f.txt",
            ZipContentKind.Text, 10, 10, 1.0, () => new MemoryStream(new byte[] { 1, 2, 3 }),
            null, "/tmp/a.zip", 200);

        // Act
        using var stream = record.OpenStream();

        // Assert
        stream.Should().NotBeNull();
        stream.Length.Should().Be(3);
    }

    [Fact]
    public void WithExpression_CreatesCopyWithModifiedField()
    {
        // Arrange
        var original = new ZipFileRecord("f.txt", null, "f.txt",
            ZipContentKind.Text, 100, 50, 2.0, DummyOpener, null, "/tmp/a.zip", 200);
        var newParsed = new Dictionary<string, object> { ["type"] = "text" };

        // Act
        var modified = original with { Parsed = newParsed };

        // Assert
        modified.Parsed.Should().NotBeNull();
        modified.FullPath.Should().Be(original.FullPath);
        original.Parsed.Should().BeNull();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        Func<Stream> opener = DummyOpener;
        var r1 = new ZipFileRecord("f.txt", null, "f.txt",
            ZipContentKind.Text, 100, 50, 2.0, opener, null, "/tmp/a.zip", 200);
        var r2 = new ZipFileRecord("f.txt", null, "f.txt",
            ZipContentKind.Text, 100, 50, 2.0, opener, null, "/tmp/a.zip", 200);

        // Assert
        r1.Should().Be(r2);
    }

    [Fact]
    public void Equality_DifferentKind_AreNotEqual()
    {
        // Arrange
        Func<Stream> opener = DummyOpener;
        var r1 = new ZipFileRecord("f.txt", null, "f.txt",
            ZipContentKind.Text, 100, 50, 2.0, opener, null, "/tmp/a.zip", 200);
        var r2 = new ZipFileRecord("f.txt", null, "f.txt",
            ZipContentKind.Binary, 100, 50, 2.0, opener, null, "/tmp/a.zip", 200);

        // Assert
        r1.Should().NotBe(r2);
    }

    [Fact]
    public void CompressionRatio_CanBeInfinity()
    {
        // Arrange & Act
        var record = new ZipFileRecord("f.txt", null, "f.txt",
            ZipContentKind.Text, 100, 0, double.PositiveInfinity, DummyOpener, null, "/tmp/a.zip", 200);

        // Assert
        double.IsPositiveInfinity(record.CompressionRatio).Should().BeTrue();
    }
}
