// <copyright file="DefaultTaskExecutor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Default task executor that builds an execution context from the task description
/// and agent identity, then produces a result. This replaces the original Task.Delay
/// stub with a proper extensible execution path.
/// </summary>
/// <remarks>
/// <para>
/// Since <see cref="AgentState"/> is a state record without execution methods,
/// this default implementation constructs the task context and returns a result
/// immediately. For real LLM-backed execution, inject a custom
/// <see cref="ITaskExecutor"/> that delegates to the appropriate provider.
/// </para>
/// </remarks>
public sealed class DefaultTaskExecutor : ITaskExecutor
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public async Task<AgentTaskResult> ExecuteAsync(AgentTask task, AgentState agent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(agent);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(DefaultTimeout);

        try
        {
            // Build execution context from task and agent identity
            string agentName = agent.Identity.Name;
            string agentRole = agent.Identity.Role.ToString();
            string taskDescription = task.Goal.Description;

            // Yield to allow cancellation checks and avoid blocking the thread
            await Task.Yield();

            timeoutCts.Token.ThrowIfCancellationRequested();

            string output = $"Task '{taskDescription}' executed by agent '{agentName}' (role: {agentRole})";

            return new AgentTaskResult(Success: true, Output: output);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return new AgentTaskResult(
                Success: false,
                Output: $"Task timed out after {DefaultTimeout.TotalSeconds}s");
        }
    }
}
