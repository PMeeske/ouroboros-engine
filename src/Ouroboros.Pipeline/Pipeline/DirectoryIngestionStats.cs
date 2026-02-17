namespace Ouroboros.Pipeline.Ingestion;

public sealed class DirectoryIngestionStats
{
    public int FilesLoaded { get; set; }
    public int SkippedUnchanged { get; set; }
    public int Errors { get; set; }
    public int VectorsProduced { get; set; }
    public TimeSpan Elapsed { get; set; }
    /// <inheritdoc/>
    public override string ToString() => $"files={FilesLoaded} skipped={SkippedUnchanged} errors={Errors} vectors={VectorsProduced} elapsed={Elapsed.TotalMilliseconds:F0}ms";
}