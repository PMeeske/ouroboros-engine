namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Defines high-level role classifications for agents in multi-agent collaboration.
/// </summary>
public enum AgentRole
{
    /// <summary>
    /// An agent specialized in analyzing information and providing insights.
    /// </summary>
    Analyst,

    /// <summary>
    /// An agent specialized in writing and modifying code.
    /// </summary>
    Coder,

    /// <summary>
    /// An agent specialized in reviewing work and providing feedback.
    /// </summary>
    Reviewer,

    /// <summary>
    /// An agent specialized in creating plans and strategies.
    /// </summary>
    Planner,

    /// <summary>
    /// An agent specialized in executing tasks and actions.
    /// </summary>
    Executor,

    /// <summary>
    /// An agent with specialized domain expertise.
    /// </summary>
    Specialist
}