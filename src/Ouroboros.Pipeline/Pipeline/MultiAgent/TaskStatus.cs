namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Represents the status of an agent task during coordination.
/// </summary>
public enum TaskStatus
{
    /// <summary>
    /// The task is waiting to be assigned to an agent.
    /// </summary>
    Pending,

    /// <summary>
    /// The task has been assigned to an agent but not yet started.
    /// </summary>
    Assigned,

    /// <summary>
    /// The task is currently being executed by an agent.
    /// </summary>
    InProgress,

    /// <summary>
    /// The task has been completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The task has failed during execution.
    /// </summary>
    Failed,

    /// <summary>
    /// The task was cancelled before completion.
    /// </summary>
    Cancelled
}