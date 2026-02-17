using System.IO.Compression;

namespace Ouroboros.Pipeline.Ingestion.Zip;

/// <summary>
/// Provides streaming enumeration of zip archive entries for memory-efficient processing.
/// </summary>
public static class ZipIngestionStreaming
{
    /// <summary>
    /// Asynchronously enumerates files in a zip archive with applied size and compression limits.
    /// This method provides streaming access without loading all entries into memory at once.
    /// </summary>
    /// <param name="zipPath">The file path to the zip archive to enumerate.</param>
    /// <param name="maxTotalBytes">Maximum total uncompressed bytes allowed across all entries (default: 500 MB).</param>
    /// <param name="maxCompressionRatio">Maximum allowed compression ratio to detect zip bombs (default: 200).</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>An async enumerable of zip file records.</returns>
    public static async IAsyncEnumerable<ZipFileRecord> EnumerateAsync(string zipPath,
        long maxTotalBytes = 500 * 1024 * 1024,
        double maxCompressionRatio = 200d,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // Ensure asynchronous nature even if iteration is fast
        await Task.Yield();
        using FileStream fs = File.OpenRead(zipPath);
        using ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);
        long total = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name)) continue;
            total += entry.Length;
            if (total > maxTotalBytes)
                yield break; // stop early
            string full = entry.FullName.Replace('\\', '/');
            string? dir = full.Contains('/') ? Path.GetDirectoryName(full)?.Replace('\\', '/') : null;
            string file = entry.Name;
            string ext = Path.GetExtension(file).ToLowerInvariant();
            ZipContentKind kind = ext switch
            {
                ".csv" => ZipContentKind.Csv,
                ".xml" => ZipContentKind.Xml,
                ".txt" => ZipContentKind.Text,
                _ => ZipContentKind.Binary
            };
            long compressed = entry.CompressedLength;
            double ratio = compressed == 0 ? double.PositiveInfinity : (double)entry.Length / compressed;
            if (ratio > maxCompressionRatio) kind = ZipContentKind.Binary;
            else if (kind == ZipContentKind.Binary && entry.Length > 0 && string.IsNullOrEmpty(ext))
            {
                try
                {
                    using Stream ps = entry.Open();
                    int toRead = (int)Math.Min(2048, entry.Length);
                    byte[] buf = new byte[toRead];
                    int read = ps.Read(buf, 0, toRead);
                    if (ZipIngestionStreamingHelpers.IsLikelyText(buf.AsSpan(0, read))) kind = ZipContentKind.Text;
                }
                catch { }
            }
            ZipArchiveEntry captured = entry;
            Func<Stream> opener = () => captured.Open();
            yield return new ZipFileRecord(full, dir, file, kind, entry.Length, compressed, ratio, opener, null, zipPath, maxCompressionRatio);
        }
    }
}