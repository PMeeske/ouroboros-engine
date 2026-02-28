// <copyright file="ITaskExecutor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Executes agent tasks, replacing the stub delay with real work dispatch.
/// Implementations can delegate to LLM providers, local execution engines,
/// or any other task processing backend.
/// </summary>
public interface ITaskExecutor
{
    /// <summary>
    /// Executes the specified task using the given agent.
    /// </summary>
    /// <param name="task">The agent task to execute.</param>
    /// <param name="agent">The agent state representing the assigned agent.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A result indicating success or failure along with output text.</returns>
    Task<AgentTaskResult> ExecuteAsync(AgentTask task, AgentState agent, CancellationToken ct);
}

/// <summary>
/// Result of an agent task execution.
/// </summary>
/// <param name="Success">Whether the task executed successfully.</param>
/// <param name="Output">The output or error message from execution.</param>
public sealed record AgentTaskResult(bool Success, string Output);
