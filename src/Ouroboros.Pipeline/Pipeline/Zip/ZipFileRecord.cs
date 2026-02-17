namespace Ouroboros.Pipeline.Ingestion.Zip;

/// <summary>
/// Represents metadata and access to a file within a zip archive.
/// </summary>
/// <param name="FullPath">The full path of the file within the zip archive.</param>
/// <param name="Directory">The directory path of the file, if any.</param>
/// <param name="FileName">The name of the file.</param>
/// <param name="Kind">The classified content type of the file.</param>
/// <param name="Length">The uncompressed size of the file in bytes.</param>
/// <param name="CompressedLength">The compressed size of the file in bytes.</param>
/// <param name="CompressionRatio">The compression ratio (uncompressed/compressed).</param>
/// <param name="OpenStream">A function to open a stream to the file's content.</param>
/// <param name="Parsed">Optional parsed content metadata, populated after parsing.</param>
/// <param name="ZipPath">The file path to the underlying zip archive for lifecycle management.</param>
/// <param name="MaxCompressionRatioLimit">The compression ratio limit that was applied during scan.</param>
public sealed record ZipFileRecord(
    string FullPath,
    string? Directory,
    string FileName,
    ZipContentKind Kind,
    long Length,
    long CompressedLength,
    double CompressionRatio,
    Func<Stream> OpenStream,
    IDictionary<string, object>? Parsed,
    string ZipPath, // path to underlying zip for lifecycle management
    double MaxCompressionRatioLimit // the limit that was applied during scan
);