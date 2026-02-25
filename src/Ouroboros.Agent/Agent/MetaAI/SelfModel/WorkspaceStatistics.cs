namespace Ouroboros.Agent.MetaAI.SelfModel;

/// <summary>
/// Statistics about the global workspace.
/// </summary>
public sealed record WorkspaceStatistics(
    int TotalItems,
    int HighPriorityItems,
    int CriticalItems,
    int ExpiredItems,
    double AverageAttentionWeight,
    Dictionary<string, int> ItemsBySource);