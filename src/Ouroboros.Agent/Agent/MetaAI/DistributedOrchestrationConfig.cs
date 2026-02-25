namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for distributed orchestration.
/// </summary>
public sealed record DistributedOrchestrationConfig(
    int MaxAgents = 10,
    TimeSpan HeartbeatTimeout = default,
    bool EnableLoadBalancing = true);