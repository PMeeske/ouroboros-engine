namespace Ouroboros.Pipeline.Ingestion;

/// <summary>
/// Controls how a directory is scanned and ingested into the vector store.
/// Pass an instance of this class to directory ingestion helpers to configure
/// file filtering, caching, and text-splitting behaviour.
/// </summary>
public sealed class DirectoryIngestionOptions
{
    /// <summary>Gets or sets a value indicating whether subdirectories are included in the scan.</summary>
    public bool Recursive { get; set; } = true;

    /// <summary>
    /// Gets or sets glob patterns used to include files (e.g. <c>["*.md", "*.txt"]</c>).
    /// When empty, all files that pass extension and size filters are included.
    /// </summary>
    public string[] Patterns { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the file extensions to include (e.g. <c>[".cs", ".md"]</c>).
    /// When <see langword="null"/>, no extension filter is applied.
    /// </summary>
    public string[]? Extensions { get; set; }

    /// <summary>
    /// Gets or sets directory names to skip during the scan (e.g. <c>["bin", "obj", "node_modules"]</c>).
    /// When <see langword="null"/>, no directories are excluded.
    /// </summary>
    public string[]? ExcludeDirectories { get; set; }

    /// <summary>
    /// Gets or sets the maximum file size in bytes to ingest. Files larger than this value are skipped.
    /// A value of <c>0</c> disables the size limit.
    /// </summary>
    public long MaxFileBytes { get; set; } = 0;

    /// <summary>
    /// Gets or sets a value indicating whether the incremental content-hash cache is disabled.
    /// When <see langword="true"/> all files are re-ingested on every run regardless of changes.
    /// </summary>
    public bool DisableCache { get; set; }

    /// <summary>Gets or sets the path where the incremental hash cache is persisted between runs.</summary>
    public string CacheFilePath { get; set; } = ".monadic_ingest_cache.json";

    /// <summary>Gets or sets the target chunk size in characters for the text splitter.</summary>
    public int ChunkSize { get; set; } = 2000;

    /// <summary>Gets or sets the number of characters that adjacent chunks share as overlap.</summary>
    public int ChunkOverlap { get; set; } = 200;
}