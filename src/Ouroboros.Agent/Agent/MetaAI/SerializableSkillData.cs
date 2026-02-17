namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Serializable skill data for Qdrant storage.
/// </summary>
internal sealed record SerializableSkillData(
    string Id,
    string Name,
    string Description,
    string Category,
    List<string> Preconditions,
    List<string> Effects,
    double SuccessRate,
    int UsageCount,
    long AverageExecutionTime,
    List<string> Tags);