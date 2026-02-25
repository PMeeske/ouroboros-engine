namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a training batch from experience replay.
/// </summary>
public sealed record TrainingBatch(
    List<Experience> Experiences,
    Dictionary<string, double> Metrics,
    DateTime CreatedAt);