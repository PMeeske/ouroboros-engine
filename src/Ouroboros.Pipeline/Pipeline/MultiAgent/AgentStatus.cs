namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Defines the possible status values for an agent in a multi-agent system.
/// </summary>
public enum AgentStatus
{
    /// <summary>
    /// The agent is idle and available to accept tasks.
    /// </summary>
    Idle,

    /// <summary>
    /// The agent is currently busy processing a task.
    /// </summary>
    Busy,

    /// <summary>
    /// The agent is waiting for external input or another agent.
    /// </summary>
    Waiting,

    /// <summary>
    /// The agent has encountered an error state.
    /// </summary>
    Error,

    /// <summary>
    /// The agent is offline and unavailable.
    /// </summary>
    Offline
}