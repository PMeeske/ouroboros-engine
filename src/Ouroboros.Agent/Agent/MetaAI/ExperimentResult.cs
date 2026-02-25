namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Result of an A/B testing experiment.
/// </summary>
public sealed record ExperimentResult(
    string ExperimentId,
    DateTime StartedAt,
    DateTime CompletedAt,
    List<VariantResult> VariantResults,
    StatisticalAnalysis? Analysis,
    string? Winner,
    ExperimentStatus Status)
{
    /// <summary>Duration of the experiment.</summary>
    public TimeSpan Duration => CompletedAt - StartedAt;

    /// <summary>Whether the experiment completed successfully.</summary>
    public bool IsCompleted => Status == ExperimentStatus.Completed;
}