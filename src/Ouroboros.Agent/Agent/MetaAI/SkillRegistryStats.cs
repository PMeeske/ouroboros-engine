namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Statistics about the skill registry.
/// </summary>
public sealed record SkillRegistryStats(
    int TotalSkills,
    double AverageSuccessRate,
    int TotalExecutions,
    string? MostUsedSkill,
    string? MostSuccessfulSkill,
    string StoragePath,
    bool IsPersisted);