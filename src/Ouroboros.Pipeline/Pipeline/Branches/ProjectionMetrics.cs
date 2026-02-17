namespace Ouroboros.Pipeline.Branches;

/// <summary>
/// Metrics for the global projection service.
/// </summary>
public sealed record ProjectionMetrics
{
    /// <summary>
    /// Gets the total number of epochs created.
    /// </summary>
    public required long TotalEpochs { get; init; }

    /// <summary>
    /// Gets the total number of branches tracked.
    /// </summary>
    public required int TotalBranches { get; init; }

    /// <summary>
    /// Gets the total number of events across all branches.
    /// </summary>
    public required long TotalEvents { get; init; }

    /// <summary>
    /// Gets the timestamp of the most recent epoch.
    /// </summary>
    public DateTime? LastEpochAt { get; init; }

    /// <summary>
    /// Gets the average events per branch.
    /// </summary>
    public double AverageEventsPerBranch => TotalBranches > 0
        ? (double)TotalEvents / TotalBranches
        : 0.0;

    /// <summary>
    /// Gets additional custom metrics.
    /// </summary>
    public IReadOnlyDictionary<string, object> CustomMetrics { get; init; } = new Dictionary<string, object>();
}