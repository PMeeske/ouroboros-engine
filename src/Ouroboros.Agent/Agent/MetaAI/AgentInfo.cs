namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents an agent in the distributed system.
/// </summary>
public sealed record AgentInfo(
    string AgentId,
    string Name,
    HashSet<string> Capabilities,
    AgentStatus Status,
    DateTime LastHeartbeat);