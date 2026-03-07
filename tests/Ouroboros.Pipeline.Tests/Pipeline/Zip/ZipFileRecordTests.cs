namespace Ouroboros.Tests.Pipeline.Zip;

using Ouroboros.Pipeline.Ingestion.Zip;

[Trait("Category", "Unit")]
public class ZipFileRecordTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var record = new ZipFileRecord(
            FullPath: "data/file.csv",
            Directory: "data",
            FileName: "file.csv",
            Kind: ZipContentKind.Csv,
            Length: 1000,
            CompressedLength: 500,
            CompressionRatio: 2.0,
            OpenStream: () => new MemoryStream(),
            Parsed: null,
            ZipPath: "/tmp/test.zip",
            MaxCompressionRatioLimit: 100.0);

        record.FullPath.Should().Be("data/file.csv");
        record.Directory.Should().Be("data");
        record.FileName.Should().Be("file.csv");
        record.Kind.Should().Be(ZipContentKind.Csv);
        record.Length.Should().Be(1000);
        record.CompressedLength.Should().Be(500);
        record.CompressionRatio.Should().Be(2.0);
        record.Parsed.Should().BeNull();
        record.ZipPath.Should().Be("/tmp/test.zip");
        record.MaxCompressionRatioLimit.Should().Be(100.0);
    }

    [Fact]
    public void OpenStream_ReturnsStreamWhenInvoked()
    {
        var record = new ZipFileRecord(
            "path", null, "file", ZipContentKind.Text, 100, 50, 2.0,
            () => new MemoryStream(new byte[] { 1, 2, 3 }),
            null, "zip", 100.0);

        using var stream = record.OpenStream();
        stream.Should().NotBeNull();
        stream.Length.Should().Be(3);
    }
}
