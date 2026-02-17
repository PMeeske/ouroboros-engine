namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Statistics about the Qdrant skill registry.
/// </summary>
public sealed record QdrantSkillRegistryStats(
    int TotalSkills,
    double AverageSuccessRate,
    int TotalExecutions,
    string? MostUsedSkill,
    string? MostSuccessfulSkill,
    string ConnectionString,
    string CollectionName,
    bool IsConnected);