namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents training result from experience replay.
/// </summary>
public sealed record TrainingResult(
    int ExperiencesProcessed,
    Dictionary<string, double> ImprovedMetrics,
    List<string> LearnedPatterns,
    bool Success);