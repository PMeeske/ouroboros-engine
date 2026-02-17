using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Represents a task assigned to an agent for execution during coordination.
/// Tasks are immutable and track their lifecycle from creation through completion.
/// </summary>
/// <param name="Id">The unique identifier for this task.</param>
/// <param name="Goal">The goal that this task is working towards.</param>
/// <param name="AssignedAgentId">The ID of the agent assigned to this task, if any.</param>
/// <param name="Status">The current status of this task.</param>
/// <param name="CreatedAt">The timestamp when this task was created.</param>
/// <param name="StartedAt">The timestamp when this task started execution, if started.</param>
/// <param name="CompletedAt">The timestamp when this task completed, if completed.</param>
/// <param name="Result">The result of the task execution, if completed successfully.</param>
/// <param name="Error">The error message if the task failed.</param>
public sealed record AgentTask(
    Guid Id,
    Goal Goal,
    Guid? AssignedAgentId,
    TaskStatus Status,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    Option<string> Result,
    Option<string> Error)
{
    /// <summary>
    /// Gets the duration of the task execution if the task has both started and completed.
    /// </summary>
    /// <value>The duration of execution, or null if the task hasn't completed.</value>
    public TimeSpan? Duration
    {
        get
        {
            if (StartedAt.HasValue && CompletedAt.HasValue)
            {
                return CompletedAt.Value - StartedAt.Value;
            }

            return null;
        }
    }

    /// <summary>
    /// Creates a new agent task for the specified goal.
    /// </summary>
    /// <param name="goal">The goal that this task will work towards.</param>
    /// <returns>A new <see cref="AgentTask"/> in pending status.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="goal"/> is null.</exception>
    public static AgentTask Create(Goal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);

        return new AgentTask(
            Id: Guid.NewGuid(),
            Goal: goal,
            AssignedAgentId: null,
            Status: TaskStatus.Pending,
            CreatedAt: DateTime.UtcNow,
            StartedAt: null,
            CompletedAt: null,
            Result: Option<string>.None(),
            Error: Option<string>.None());
    }

    /// <summary>
    /// Creates a new task with the specified agent assigned.
    /// </summary>
    /// <param name="agentId">The ID of the agent to assign to this task.</param>
    /// <returns>A new <see cref="AgentTask"/> with the agent assigned and status set to Assigned.</returns>
    public AgentTask AssignTo(Guid agentId)
    {
        return this with
        {
            AssignedAgentId = agentId,
            Status = TaskStatus.Assigned
        };
    }

    /// <summary>
    /// Creates a new task marked as in progress with the current timestamp.
    /// </summary>
    /// <returns>A new <see cref="AgentTask"/> with status set to InProgress and StartedAt timestamp.</returns>
    public AgentTask Start()
    {
        return this with
        {
            Status = TaskStatus.InProgress,
            StartedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new task marked as completed with the specified result.
    /// </summary>
    /// <param name="result">The result of the task execution.</param>
    /// <returns>A new <see cref="AgentTask"/> with status set to Completed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
    public AgentTask Complete(string result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return this with
        {
            Status = TaskStatus.Completed,
            CompletedAt = DateTime.UtcNow,
            Result = Option<string>.Some(result)
        };
    }

    /// <summary>
    /// Creates a new task marked as failed with the specified error message.
    /// </summary>
    /// <param name="error">The error message describing the failure.</param>
    /// <returns>A new <see cref="AgentTask"/> with status set to Failed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is null.</exception>
    public AgentTask Fail(string error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return this with
        {
            Status = TaskStatus.Failed,
            CompletedAt = DateTime.UtcNow,
            Error = Option<string>.Some(error)
        };
    }
}