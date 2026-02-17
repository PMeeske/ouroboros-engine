namespace Ouroboros.Pipeline.Memory;

/// <summary>
/// Event indicating that episodes were retrieved.
/// </summary>
public sealed record EpisodesRetrievedEvent(
    Guid Id,
    int Count,
    string Query,
    DateTime Timestamp) : PipelineEvent(Id, "EpisodesRetrieved", Timestamp)
{
    /// <summary>
    /// Creates a new EpisodesRetrievedEvent with auto-generated ID and current timestamp.
    /// </summary>
    public EpisodesRetrievedEvent(int count, string query, DateTime timestamp)
        : this(Guid.NewGuid(), count, query, timestamp)
    {
    }
}