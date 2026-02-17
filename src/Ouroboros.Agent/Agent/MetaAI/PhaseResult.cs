namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Result from a single phase in the improvement cycle.
/// </summary>
/// <param name="Phase">Which phase this result is from.</param>
/// <param name="Success">Whether the phase succeeded.</param>
/// <param name="Output">The phase output.</param>
/// <param name="Error">Error message if failed.</param>
/// <param name="Duration">Phase execution duration.</param>
/// <param name="Metadata">Additional metadata.</param>
public sealed record PhaseResult(
    ImprovementPhase Phase,
    bool Success,
    string Output,
    string? Error,
    TimeSpan Duration,
    Dictionary<string, object>? Metadata = null)
{
    /// <summary>
    /// Gets the metadata dictionary, never null.
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = Metadata ?? new Dictionary<string, object>();
}