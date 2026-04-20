using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Ouroboros.Pipeline.Ingestion.Zip;

namespace Ouroboros.Tests.Zip;

[Trait("Category", "Unit")]
public class ZipIngestionTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _tempFiles = new();

    public ZipIngestionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ZipIngestionTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { ZipArchiveRegistry.Release(file); } catch { /* best effort */ }
            try { File.Delete(file); } catch { /* best effort */ }
        }
        try { Directory.Delete(_tempDir, true); } catch { /* best effort */ }
    }

    private string CreateTestZip(params (string name, byte[] content)[] entries)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".zip");
        using (var fs = File.Create(path))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var stream = entry.Open();
                stream.Write(content, 0, content.Length);
            }
        }
        _tempFiles.Add(path);
        return path;
    }

    private string CreateTextZip(params (string name, string content)[] entries)
    {
        return CreateTestZip(entries.Select(e =>
            (e.name, Encoding.UTF8.GetBytes(e.content))).ToArray());
    }

    #region ScanAsync

    [Fact]
    public async Task ScanAsync_EmptyZip_ReturnsEmptyList()
    {
        // Arrange
        var path = CreateTextZip();

        // Act
        var results = await ZipIngestion.ScanAsync(path);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_SingleTextFile_ReturnsOneRecord()
    {
        // Arrange
        var path = CreateTextZip(("hello.txt", "Hello World"));

        // Act
        var results = await ZipIngestion.ScanAsync(path);

        // Assert
        results.Should().HaveCount(1);
        results[0].FileName.Should().Be("hello.txt");
        results[0].Kind.Should().Be(ZipContentKind.Text);
        results[0].FullPath.Should().Be("hello.txt");
    }

    [Fact]
    public async Task ScanAsync_CsvFile_ClassifiedAsCsv()
    {
        // Arrange
        var path = CreateTextZip(("data.csv", "Name,Age\nAlice,30"));

        // Act
        var results = await ZipIngestion.ScanAsync(path);

        // Assert
        results[0].Kind.Should().Be(ZipContentKind.Csv);
    }

    [Fact]
    public async Task ScanAsync_XmlFile_ClassifiedAsXml()
    {
        // Arrange
        var path = CreateTextZip(("config.xml", "<root><item/></root>"));

        // Act
        var results = await ZipIngestion.ScanAsync(path);

        // Assert
        results[0].Kind.Should().Be(ZipContentKind.Xml);
    }

    [Fact]
    public async Task ScanAsync_UnknownExtension_ClassifiedAsBinary()
    {
        // Arrange
        var path = CreateTestZip(("image.png", new byte[] { 0x89, 0x50, 0x4E, 0x47 }));

        // Act
        var results = await ZipIngestion.ScanAsync(path);

        // Assert
        results[0].Kind.Should().Be(ZipContentKind.Binary);
    }

    [Fact]
    public async Task ScanAsync_MultipleFiles_ReturnsAllRecords()
    {
        // Arrange
        var path = CreateTextZip(
            ("file1.txt", "text content"),
            ("file2.csv", "a,b\n1,2"),
            ("file3.xml", "<root/>"));

        // Act
        var results = await ZipIngestion.ScanAsync(path);

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task ScanAsync_FileInSubdirectory_SetsDirectoryAndFullPath()
    {
        // Arrange
        var path = CreateTextZip(("sub/dir/file.txt", "nested"));

        // Act
        var results = await ZipIngestion.ScanAsync(path);

        // Assert
        results[0].FullPath.Should().Be("sub/dir/file.txt");
        results[0].Directory.Should().Be("sub/dir");
        results[0].FileName.Should().Be("file.txt");
    }

    [Fact]
    public async Task ScanAsync_FileAtRoot_HasNullDirectory()
    {
        // Arrange
        var path = CreateTextZip(("root.txt", "at root"));

        // Act
        var results = await ZipIngestion.ScanAsync(path);

        // Assert
        results[0].Directory.Should().BeNull();
    }

    [Fact]
    public async Task ScanAsync_ExceedsTotalBytes_ThrowsInvalidOperationException()
    {
        // Arrange
        var largeContent = new byte[1024];
        var path = CreateTestZip(("large.bin", largeContent));

        // Act
        var act = () => ZipIngestion.ScanAsync(path, maxTotalBytes: 100);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds*");
    }

    [Fact]
    public async Task ScanAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var path = CreateTextZip(("f1.txt", "a"), ("f2.txt", "b"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => ZipIngestion.ScanAsync(path, ct: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ScanAsync_SetsZipPathOnRecords()
    {
        // Arrange
        var path = CreateTextZip(("test.txt", "content"));

        // Act
        var results = await ZipIngestion.ScanAsync(path);

        // Assert
        results[0].ZipPath.Should().Be(path);
    }

    [Fact]
    public async Task ScanAsync_SetsMaxCompressionRatioLimitOnRecords()
    {
        // Arrange
        var path = CreateTextZip(("test.txt", "content"));

        // Act
        var results = await ZipIngestion.ScanAsync(path, maxCompressionRatio: 150);

        // Assert
        results[0].MaxCompressionRatioLimit.Should().Be(150);
    }

    [Fact]
    public async Task ScanAsync_CompressionRatio_CalculatedCorrectly()
    {
        // Arrange
        var path = CreateTextZip(("test.txt", "some content here"));

        // Act
        var results = await ZipIngestion.ScanAsync(path);

        // Assert
        results[0].CompressionRatio.Should().BeGreaterThan(0);
        results[0].Length.Should().BeGreaterThan(0);
    }

    #endregion

    #region ParseAsync

    [Fact]
    public async Task ParseAsync_TextFile_ParsesCorrectly()
    {
        // Arrange
        var path = CreateTextZip(("hello.txt", "Hello World"));
        var records = await ZipIngestion.ScanAsync(path);

        // Act
        var parsed = await ZipIngestion.ParseAsync(records);

        // Assert
        parsed.Should().HaveCount(1);
        parsed[0].Parsed.Should().NotBeNull();
        parsed[0].Parsed!["type"].Should().Be("text");
        parsed[0].Parsed["preview"].Should().Be("Hello World");
    }

    [Fact]
    public async Task ParseAsync_CsvFile_ParsesHeaderAndRows()
    {
        // Arrange
        var csvContent = "Name,Age,City\nAlice,30,NYC\nBob,25,LA";
        var path = CreateTextZip(("data.csv", csvContent));
        var records = await ZipIngestion.ScanAsync(path);

        // Act
        var parsed = await ZipIngestion.ParseAsync(records);

        // Assert
        parsed[0].Parsed.Should().NotBeNull();
        parsed[0].Parsed!["type"].Should().Be("csv");
        var table = parsed[0].Parsed["table"] as CsvTable;
        table.Should().NotBeNull();
        table!.Header.Should().BeEquivalentTo(new[] { "Name", "Age", "City" });
        table.Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task ParseAsync_CsvFile_RespectsMaxLines()
    {
        // Arrange
        var sb = new StringBuilder("Col1,Col2\n");
        for (int i = 0; i < 100; i++)
            sb.AppendLine($"val{i},data{i}");
        var path = CreateTextZip(("big.csv", sb.ToString()));
        var records = await ZipIngestion.ScanAsync(path);

        // Act
        var parsed = await ZipIngestion.ParseAsync(records, csvMaxLines: 5);

        // Assert
        var table = parsed[0].Parsed!["table"] as CsvTable;
        table!.Rows.Should().HaveCount(5);
        parsed[0].Parsed["truncated"].Should().Be(true);
    }

    [Fact]
    public async Task ParseAsync_XmlFile_ParsesDocument()
    {
        // Arrange
        var xmlContent = "<?xml version=\"1.0\"?><root><item name=\"a\">text1</item><item name=\"b\">text2</item></root>";
        var path = CreateTextZip(("doc.xml", xmlContent));
        var records = await ZipIngestion.ScanAsync(path);

        // Act
        var parsed = await ZipIngestion.ParseAsync(records);

        // Assert
        parsed[0].Parsed.Should().NotBeNull();
        parsed[0].Parsed!["type"].Should().Be("xml");
        parsed[0].Parsed["root"].Should().Be("root");
        ((int)parsed[0].Parsed["elementCount"]).Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ParseAsync_XmlFile_IncludesTextPreview()
    {
        // Arrange
        var xmlContent = "<root>hello world</root>";
        var path = CreateTextZip(("doc.xml", xmlContent));
        var records = await ZipIngestion.ScanAsync(path);

        // Act
        var parsed = await ZipIngestion.ParseAsync(records, includeXmlText: true);

        // Assert
        parsed[0].Parsed!["textPreview"].Should().Be("hello world");
    }

    [Fact]
    public async Task ParseAsync_XmlFile_ExcludesTextPreviewWhenDisabled()
    {
        // Arrange
        var xmlContent = "<root>hello world</root>";
        var path = CreateTextZip(("doc.xml", xmlContent));
        var records = await ZipIngestion.ScanAsync(path);

        // Act
        var parsed = await ZipIngestion.ParseAsync(records, includeXmlText: false);

        // Assert
        parsed[0].Parsed!["textPreview"].Should().Be(string.Empty);
    }

    [Fact]
    public async Task ParseAsync_BinaryFile_ReturnsHashAndSize()
    {
        // Arrange
        var binaryContent = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE, 0xFD };
        var path = CreateTestZip(("data.bin", binaryContent));
        var records = await ZipIngestion.ScanAsync(path);

        // Act
        var parsed = await ZipIngestion.ParseAsync(records);

        // Assert
        parsed[0].Parsed.Should().NotBeNull();
        parsed[0].Parsed!["type"].Should().Be("binary");
        parsed[0].Parsed["sha256"].Should().NotBeNull();
        ((string)parsed[0].Parsed["sha256"]).Should().NotBeEmpty();
    }

    [Fact]
    public async Task ParseAsync_EmptyCsvFile_ReturnsEmptyMarker()
    {
        // Arrange
        var path = CreateTextZip(("empty.csv", ""));
        var records = await ZipIngestion.ScanAsync(path);

        // Act
        var parsed = await ZipIngestion.ParseAsync(records);

        // Assert
        parsed[0].Parsed.Should().NotBeNull();
        parsed[0].Parsed!["type"].Should().Be("csv");
    }

    [Fact]
    public async Task ParseAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var path = CreateTextZip(("test.txt", "content"));
        var records = await ZipIngestion.ScanAsync(path);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => ZipIngestion.ParseAsync(records, ct: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ParseAsync_HighCompressionRatio_SkipsWithReason()
    {
        // Arrange - create a record with ratio exceeding the limit
        var path = CreateTextZip(("test.txt", "content"));
        var records = await ZipIngestion.ScanAsync(path, maxCompressionRatio: 0.001);

        // Only test if any record actually exceeds the ratio
        // For small test files, the ratio might not exceed 0.001
        // So we manually construct a record with a high ratio
        var fakeRecord = new ZipFileRecord(
            "bomb.txt", null, "bomb.txt", ZipContentKind.Text,
            Length: 1_000_000, CompressedLength: 1,
            CompressionRatio: 1_000_000,
            OpenStream: () => new MemoryStream(Encoding.UTF8.GetBytes("test")),
            Parsed: null, ZipPath: path,
            MaxCompressionRatioLimit: 200);

        // Act
        var parsed = await ZipIngestion.ParseAsync(new[] { fakeRecord });

        // Assert
        parsed[0].Parsed.Should().NotBeNull();
        parsed[0].Parsed!["type"].Should().Be("skipped");
        parsed[0].Parsed["reason"].Should().Be("compression-ratio-exceeded");
    }

    [Fact]
    public async Task ParseAsync_InfiniteCompressionRatio_IsNotSkipped()
    {
        // Arrange - infinite ratio (compressed = 0) is a special case
        var fakeRecord = new ZipFileRecord(
            "zero.txt", null, "zero.txt", ZipContentKind.Text,
            Length: 100, CompressedLength: 0,
            CompressionRatio: double.PositiveInfinity,
            OpenStream: () => new MemoryStream(Encoding.UTF8.GetBytes("hello")),
            Parsed: null, ZipPath: "fake.zip",
            MaxCompressionRatioLimit: 200);

        // Act
        var parsed = await ZipIngestion.ParseAsync(new[] { fakeRecord });

        // Assert - infinity is not considered to exceed the ratio limit
        parsed[0].Parsed!["type"].Should().Be("text");
    }

    [Fact]
    public async Task ParseAsync_CsvWithQuotedFields_ParsesCorrectly()
    {
        // Arrange
        var csvContent = "Name,Description\n\"Alice\",\"She said \"\"hello\"\"\"\nBob,Simple";
        var path = CreateTextZip(("quoted.csv", csvContent));
        var records = await ZipIngestion.ScanAsync(path);

        // Act
        var parsed = await ZipIngestion.ParseAsync(records);

        // Assert
        var table = parsed[0].Parsed!["table"] as CsvTable;
        table.Should().NotBeNull();
        table!.Header.Should().BeEquivalentTo(new[] { "Name", "Description" });
        table.Rows[0][1].Should().Contain("hello");
    }

    [Fact]
    public async Task ParseAsync_CsvWithCommasInQuotes_ParsesCorrectly()
    {
        // Arrange
        var csvContent = "City,Population\n\"New York, NY\",8000000";
        var path = CreateTextZip(("cities.csv", csvContent));
        var records = await ZipIngestion.ScanAsync(path);

        // Act
        var parsed = await ZipIngestion.ParseAsync(records);

        // Assert
        var table = parsed[0].Parsed!["table"] as CsvTable;
        table!.Rows[0][0].Should().Be("New York, NY");
    }

    [Fact]
    public async Task ParseAsync_TextFile_RespectsMaxBytes()
    {
        // Arrange
        var largeText = new string('A', 10_000);
        var path = CreateTextZip(("large.txt", largeText));
        var records = await ZipIngestion.ScanAsync(path);

        // Act
        var parsed = await ZipIngestion.ParseAsync(records, binaryMaxBytes: 100);

        // Assert
        var preview = (string)parsed[0].Parsed!["preview"];
        preview.Length.Should().BeLessThanOrEqualTo(100);
        parsed[0].Parsed["truncated"].Should().Be(true);
    }

    [Fact]
    public async Task ParseAsync_PreservesOriginalRecordFields()
    {
        // Arrange
        var path = CreateTextZip(("test.txt", "content"));
        var records = await ZipIngestion.ScanAsync(path);
        var original = records[0];

        // Act
        var parsed = await ZipIngestion.ParseAsync(records);

        // Assert
        parsed[0].FullPath.Should().Be(original.FullPath);
        parsed[0].FileName.Should().Be(original.FileName);
        parsed[0].Kind.Should().Be(original.Kind);
        parsed[0].Length.Should().Be(original.Length);
    }

    [Fact]
    public async Task ParseAsync_MultipleFiles_ParsesAll()
    {
        // Arrange
        var path = CreateTextZip(
            ("file.txt", "text content"),
            ("data.csv", "A,B\n1,2"),
            ("doc.xml", "<root/>"));
        var records = await ZipIngestion.ScanAsync(path);

        // Act
        var parsed = await ZipIngestion.ParseAsync(records);

        // Assert
        parsed.Should().HaveCount(3);
        parsed.All(r => r.Parsed != null).Should().BeTrue();
    }

    #endregion
}
