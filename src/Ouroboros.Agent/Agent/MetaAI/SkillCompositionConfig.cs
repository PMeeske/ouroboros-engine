namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for skill composition.
/// </summary>
public sealed record SkillCompositionConfig(
    int MaxComponentSkills = 5,
    double MinComponentQuality = 0.7,
    bool AllowRecursiveComposition = false);