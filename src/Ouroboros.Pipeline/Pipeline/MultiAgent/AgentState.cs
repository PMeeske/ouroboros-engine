namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Represents the current state of an agent, including status and task statistics.
/// </summary>
/// <param name="Identity">The identity of the agent.</param>
/// <param name="Status">The current status of the agent.</param>
/// <param name="CurrentTaskId">The ID of the task currently being processed, if any.</param>
/// <param name="CompletedTasks">The number of tasks successfully completed by this agent.</param>
/// <param name="FailedTasks">The number of tasks that failed during processing by this agent.</param>
/// <param name="LastActivityAt">The timestamp of the agent's last activity.</param>
public sealed record AgentState(
    AgentIdentity Identity,
    AgentStatus Status,
    Option<Guid> CurrentTaskId,
    int CompletedTasks,
    int FailedTasks,
    DateTime LastActivityAt)
{
    /// <summary>
    /// Gets the success rate of this agent based on completed and failed tasks.
    /// </summary>
    /// <value>A value between 0.0 and 1.0 representing the success rate, or 1.0 if no tasks have been attempted.</value>
    public double SuccessRate
    {
        get
        {
            int totalTasks = CompletedTasks + FailedTasks;
            return totalTasks > 0 ? (double)CompletedTasks / totalTasks : 1.0;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this agent is available to accept new tasks.
    /// </summary>
    /// <value><c>true</c> if the agent is idle; otherwise, <c>false</c>.</value>
    public bool IsAvailable => Status == AgentStatus.Idle;

    /// <summary>
    /// Creates a new agent state for the specified agent identity.
    /// </summary>
    /// <param name="identity">The identity of the agent.</param>
    /// <returns>A new <see cref="AgentState"/> instance in idle status with no completed or failed tasks.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="identity"/> is null.</exception>
    public static AgentState ForAgent(AgentIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return new AgentState(
            identity,
            AgentStatus.Idle,
            Option<Guid>.None(),
            CompletedTasks: 0,
            FailedTasks: 0,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a new agent state with the specified status.
    /// </summary>
    /// <param name="status">The new status for the agent.</param>
    /// <returns>A new <see cref="AgentState"/> instance with the updated status and activity timestamp.</returns>
    public AgentState WithStatus(AgentStatus status)
    {
        return this with
        {
            Status = status,
            LastActivityAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new agent state indicating the agent has started processing a task.
    /// </summary>
    /// <param name="taskId">The ID of the task being started.</param>
    /// <returns>A new <see cref="AgentState"/> instance with busy status and the current task ID set.</returns>
    public AgentState StartTask(Guid taskId)
    {
        return this with
        {
            Status = AgentStatus.Busy,
            CurrentTaskId = Option<Guid>.Some(taskId),
            LastActivityAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new agent state indicating the current task was completed successfully.
    /// </summary>
    /// <returns>A new <see cref="AgentState"/> instance with idle status, incremented completed tasks, and cleared current task.</returns>
    public AgentState CompleteTask()
    {
        return this with
        {
            Status = AgentStatus.Idle,
            CurrentTaskId = Option<Guid>.None(),
            CompletedTasks = CompletedTasks + 1,
            LastActivityAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new agent state indicating the current task failed.
    /// </summary>
    /// <returns>A new <see cref="AgentState"/> instance with error status, incremented failed tasks, and cleared current task.</returns>
    public AgentState FailTask()
    {
        return this with
        {
            Status = AgentStatus.Error,
            CurrentTaskId = Option<Guid>.None(),
            FailedTasks = FailedTasks + 1,
            LastActivityAt = DateTime.UtcNow
        };
    }
}