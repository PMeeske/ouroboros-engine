namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for experience replay.
/// </summary>
public sealed record ExperienceReplayConfig(
    int BatchSize = 10,
    double MinQualityScore = 0.6,
    int MaxExperiences = 100,
    bool PrioritizeHighQuality = true);