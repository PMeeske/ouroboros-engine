namespace Ouroboros.Agent.MetaAI.SelfModel;

/// <summary>
/// Represents a resource tracked by the agent.
/// </summary>
public sealed record AgentResource(
    string Name,
    string Type,
    double Available,
    double Total,
    string Unit,
    DateTime LastUpdated,
    Dictionary<string, object> Metadata);