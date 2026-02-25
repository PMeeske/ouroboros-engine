namespace Ouroboros.Pipeline.Memory;

/// <summary>
/// Event indicating that an episode was stored.
/// </summary>
public sealed record EpisodeStoredEvent(
    Guid Id,
    EpisodeId EpisodeId,
    string Goal,
    bool Success,
    DateTime Timestamp) : PipelineEvent(Id, "EpisodeStored", Timestamp)
{
    /// <summary>
    /// Creates a new EpisodeStoredEvent with auto-generated ID and current timestamp.
    /// </summary>
    public EpisodeStoredEvent(EpisodeId episodeId, string goal, bool success, DateTime timestamp)
        : this(Guid.NewGuid(), episodeId, goal, success, timestamp)
    {
    }
}