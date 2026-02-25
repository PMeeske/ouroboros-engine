namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Interface for distributed orchestration capabilities.
/// </summary>
public interface IDistributedOrchestrator
{
    /// <summary>
    /// Registers an agent in the distributed system.
    /// </summary>
    void RegisterAgent(AgentInfo agent);

    /// <summary>
    /// Unregisters an agent from the system.
    /// </summary>
    void UnregisterAgent(string agentId);

    /// <summary>
    /// Executes a plan across multiple agents.
    /// </summary>
    Task<Result<PlanExecutionResult, string>> ExecuteDistributedAsync(
        Plan plan,
        CancellationToken ct = default);

    /// <summary>
    /// Gets status of all registered agents.
    /// </summary>
    IReadOnlyList<AgentInfo> GetAgentStatus();

    /// <summary>
    /// Updates agent heartbeat.
    /// </summary>
    void UpdateHeartbeat(string agentId);
}