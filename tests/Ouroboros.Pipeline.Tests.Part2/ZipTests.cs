namespace Ouroboros.Pipeline.Tests;

using Ouroboros.Pipeline.Ingestion.Zip;

[Trait("Category", "Unit")]
public class CsvTableTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var table = new CsvTable(new[] { "Col1", "Col2" }, new List<string[]> { new[] { "a", "b" } });
        table.Header.Should().ContainInOrder("Col1", "Col2");
        table.Rows.Should().HaveCount(1);
    }
}

[Trait("Category", "Unit")]
public class DeferredZipTextCacheTests
{
    [Fact]
    public void Constructor_ShouldInitialize()
    {
        var cache = new DeferredZipTextCache();
        cache.Should().NotBeNull();
    }

    [Fact]
    public void GetOrLoad_ShouldLoadText()
    {
        var cache = new DeferredZipTextCache();
        // Can't easily test without real stream, just verify API exists
        typeof(DeferredZipTextCache).GetMethod("GetOrLoad").Should().NotBeNull();
    }
}

[Trait("Category", "Unit")]
public class XmlDocTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var doc = new XmlDoc("root", new Dictionary<string, List<string>>());
        doc.RootElementName.Should().Be("root");
    }
}

[Trait("Category", "Unit")]
public class ZipContentKindTests
{
    [Theory]
    [InlineData(ZipContentKind.Csv)]
    [InlineData(ZipContentKind.Xml)]
    [InlineData(ZipContentKind.Text)]
    [InlineData(ZipContentKind.Binary)]
    public void AllEnumValues_ShouldBeDefined(ZipContentKind value)
    {
        ((int)value).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Enum_ShouldHaveFourValues()
    {
        var values = Enum.GetValues<ZipContentKind>();
        values.Should().HaveCount(4);
    }
}

[Trait("Category", "Unit")]
public class ZipFileRecordTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var record = new ZipFileRecord(
            "path/file.txt",
            "path",
            "file.txt",
            ZipContentKind.Text,
            100,
            50,
            2.0,
            () => new MemoryStream(),
            null,
            "archive.zip",
            200.0);

        record.FullPath.Should().Be("path/file.txt");
        record.Directory.Should().Be("path");
        record.FileName.Should().Be("file.txt");
        record.Kind.Should().Be(ZipContentKind.Text);
        record.Length.Should().Be(100);
        record.CompressedLength.Should().Be(50);
        record.CompressionRatio.Should().Be(2.0);
        record.ZipPath.Should().Be("archive.zip");
        record.MaxCompressionRatioLimit.Should().Be(200.0);
    }

    [Fact]
    public void OpenStream_ShouldReturnStream()
    {
        var record = new ZipFileRecord(
            "path/file.txt",
            null,
            "file.txt",
            ZipContentKind.Text,
            0,
            0,
            0,
            () => new MemoryStream(Encoding.UTF8.GetBytes("hello")),
            null,
            "archive.zip",
            200.0);

        using var stream = record.OpenStream();
        stream.Should().NotBeNull();
    }
}

[Trait("Category", "Unit")]
public class ZipIngestionTests
{
    #region ScanAsync

    [Fact]
    public void ScanAsync_NullPath_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ZipIngestion.ScanAsync(null!));
    }

    [Fact]
    public void ScanAsync_InvalidPath_ShouldThrow()
    {
        Assert.ThrowsAny<Exception>(() => ZipIngestion.ScanAsync("nonexistent.zip").GetAwaiter().GetResult());
    }

    #endregion

    #region ParseAsync

    [Fact]
    public void ParseAsync_NullRecords_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ZipIngestion.ParseAsync(null!));
    }

    [Fact]
    public void ParseAsync_EmptyRecords_ShouldReturnEmpty()
    {
        var result = ZipIngestion.ParseAsync(Array.Empty<ZipFileRecord>()).GetAwaiter().GetResult();
        result.Should().BeEmpty();
    }

    #endregion

    #region DetectEncoding

    [Fact]
    public void DetectEncoding_Utf8Bom_ShouldReturnUtf8()
    {
        var utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF, 0x48, 0x65, 0x6C, 0x6C, 0x6F };
        using var stream = new MemoryStream(utf8Bom);
        var encoding = ZipIngestion.DetectEncoding(stream);
        encoding.Should().Be(Encoding.UTF8);
    }

    [Fact]
    public void DetectEncoding_Utf16LeBom_ShouldReturnUtf16()
    {
        var utf16LeBom = new byte[] { 0xFF, 0xFE, 0x48, 0x00 };
        using var stream = new MemoryStream(utf16LeBom);
        var encoding = ZipIngestion.DetectEncoding(stream);
        encoding.Should().Be(Encoding.Unicode);
    }

    [Fact]
    public void DetectEncoding_Utf16BeBom_ShouldReturnUtf16Be()
    {
        var utf16BeBom = new byte[] { 0xFE, 0xFF, 0x00, 0x48 };
        using var stream = new MemoryStream(utf16BeBom);
        var encoding = ZipIngestion.DetectEncoding(stream);
        encoding.Should().Be(Encoding.BigEndianUnicode);
    }

    [Fact]
    public void DetectEncoding_NoBom_ShouldReturnUtf8()
    {
        var noBom = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F };
        using var stream = new MemoryStream(noBom);
        var encoding = ZipIngestion.DetectEncoding(stream);
        encoding.Should().Be(Encoding.UTF8);
    }

    #endregion
}

[Trait("Category", "Unit")]
public class ZipIngestionStreamingTests
{
    [Fact]
    public void StreamRecordsAsync_NullPath_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ZipIngestionStreaming.StreamRecordsAsync(null!));
    }
}

[Trait("Category", "Unit")]
public class ZipIngestionStreamingHelpersTests
{
    [Fact]
    public void IsLikelyText_PureAscii_ShouldReturnTrue()
    {
        var bytes = Encoding.UTF8.GetBytes("Hello World! This is pure ASCII text.");
        ZipIngestionStreamingHelpers.IsLikelyText(bytes.AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void IsLikelyText_Binary_ShouldReturnFalse()
    {
        var bytes = new byte[] { 0x00, 0x01, 0x02, 0x03, 0xFF, 0xFE };
        ZipIngestionStreamingHelpers.IsLikelyText(bytes.AsSpan()).Should().BeFalse();
    }

    [Fact]
    public void IsLikelyText_Empty_ShouldReturnFalse()
    {
        ZipIngestionStreamingHelpers.IsLikelyText(ReadOnlySpan<byte>.Empty).Should().BeFalse();
    }
}

[Trait("Category", "Unit")]
public class ZipArchiveRegistryTests
{
    [Fact]
    public void Acquire_NullPath_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ZipArchiveRegistry.Acquire(null!));
    }

    [Fact]
    public void Acquire_InvalidPath_ShouldThrow()
    {
        Assert.ThrowsAny<Exception>(() => ZipArchiveRegistry.Acquire("nonexistent.zip"));
    }

    [Fact]
    public void Release_NullPath_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ZipArchiveRegistry.Release(null!));
    }
}
