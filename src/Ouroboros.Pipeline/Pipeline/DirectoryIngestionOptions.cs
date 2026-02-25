namespace Ouroboros.Pipeline.Ingestion;

public sealed class DirectoryIngestionOptions
{
    public bool Recursive { get; set; } = true;
    public string[] Patterns { get; set; } = Array.Empty<string>();
    public string[]? Extensions { get; set; }
    public string[]? ExcludeDirectories { get; set; }
    public long MaxFileBytes { get; set; } = 0;
    public bool DisableCache { get; set; }
    public string CacheFilePath { get; set; } = ".monadic_ingest_cache.json";
    public int ChunkSize { get; set; } = 2000;
    public int ChunkOverlap { get; set; } = 200;
}