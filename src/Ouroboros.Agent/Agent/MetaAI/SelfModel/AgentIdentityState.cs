namespace Ouroboros.Agent.MetaAI.SelfModel;

/// <summary>
/// Complete identity state of the agent.
/// </summary>
public sealed record AgentIdentityState(
    Guid AgentId,
    string Name,
    List<AgentCapability> Capabilities,
    List<AgentResource> Resources,
    List<AgentCommitment> Commitments,
    AgentPerformance Performance,
    DateTime StateTimestamp,
    Dictionary<string, object> Metadata);