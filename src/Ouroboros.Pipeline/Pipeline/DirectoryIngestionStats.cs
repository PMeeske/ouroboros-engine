namespace Ouroboros.Pipeline.Ingestion;

/// <summary>
/// Captures the outcome of a directory ingestion run, including file counts, errors, and timing.
/// </summary>
public sealed class DirectoryIngestionStats
{
    /// <summary>Gets or sets the number of files that were newly loaded and embedded.</summary>
    public int FilesLoaded { get; set; }

    /// <summary>Gets or sets the number of files that were skipped because their content hash matched the cache.</summary>
    public int SkippedUnchanged { get; set; }

    /// <summary>Gets or sets the number of files that failed to load or embed.</summary>
    public int Errors { get; set; }

    /// <summary>Gets or sets the total number of embedding vectors produced and added to the store.</summary>
    public int VectorsProduced { get; set; }

    /// <summary>Gets or sets the total wall-clock time taken by the ingestion run.</summary>
    public TimeSpan Elapsed { get; set; }
    /// <inheritdoc/>
    public override string ToString() => $"files={FilesLoaded} skipped={SkippedUnchanged} errors={Errors} vectors={VectorsProduced} elapsed={Elapsed.TotalMilliseconds:F0}ms";
}