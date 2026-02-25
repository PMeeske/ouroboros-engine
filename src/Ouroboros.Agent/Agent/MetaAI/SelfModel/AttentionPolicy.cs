namespace Ouroboros.Agent.MetaAI.SelfModel;

/// <summary>
/// Attention policy configuration.
/// </summary>
public sealed record AttentionPolicy(
    int MaxWorkspaceSize,
    int MaxHighPriorityItems,
    TimeSpan DefaultItemLifetime,
    double MinAttentionThreshold);