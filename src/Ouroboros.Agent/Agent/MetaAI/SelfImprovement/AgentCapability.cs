namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a capability that the agent possesses.
/// </summary>
public sealed record AgentCapability(
    string Name,
    string Description,
    List<string> RequiredTools,
    double SuccessRate,
    double AverageLatency,
    List<string> KnownLimitations,
    int UsageCount,
    DateTime CreatedAt,
    DateTime LastUsed,
    Dictionary<string, object> Metadata);