namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Serializable skill format for JSON persistence.
/// </summary>
internal sealed record SerializableSkill(
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