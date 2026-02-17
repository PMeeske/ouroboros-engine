using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Represents the result of a multi-agent coordination session.
/// Contains all tasks, participating agents, and coordination metrics.
/// </summary>
/// <param name="OriginalGoal">The original goal that was coordinated.</param>
/// <param name="Tasks">The list of tasks executed during coordination.</param>
/// <param name="ParticipatingAgents">Dictionary of agents that participated in the coordination.</param>
/// <param name="IsSuccess">Indicates whether the overall coordination was successful.</param>
/// <param name="Summary">A human-readable summary of the coordination result.</param>
/// <param name="TotalDuration">The total time spent on coordination.</param>
public sealed record CoordinationResult(
    Goal OriginalGoal,
    IReadOnlyList<AgentTask> Tasks,
    IReadOnlyDictionary<Guid, AgentIdentity> ParticipatingAgents,
    bool IsSuccess,
    string Summary,
    TimeSpan TotalDuration)
{
    /// <summary>
    /// Gets the count of tasks that completed successfully.
    /// </summary>
    /// <value>The number of completed tasks.</value>
    public int CompletedTaskCount => Tasks.Count(t => t.Status == TaskStatus.Completed);

    /// <summary>
    /// Gets the count of tasks that failed during execution.
    /// </summary>
    /// <value>The number of failed tasks.</value>
    public int FailedTaskCount => Tasks.Count(t => t.Status == TaskStatus.Failed);

    /// <summary>
    /// Gets the success rate as a ratio of completed tasks to total tasks.
    /// </summary>
    /// <value>A value between 0.0 and 1.0, or 1.0 if no tasks were executed.</value>
    public double SuccessRate
    {
        get
        {
            int totalTasks = CompletedTaskCount + FailedTaskCount;
            return totalTasks > 0 ? (double)CompletedTaskCount / totalTasks : 1.0;
        }
    }

    /// <summary>
    /// Creates a successful coordination result.
    /// </summary>
    /// <param name="goal">The goal that was coordinated.</param>
    /// <param name="tasks">The tasks that were executed.</param>
    /// <param name="agents">The agents that participated.</param>
    /// <param name="duration">The total duration of the coordination.</param>
    /// <returns>A <see cref="CoordinationResult"/> indicating success.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public static CoordinationResult Success(
        Goal goal,
        IReadOnlyList<AgentTask> tasks,
        IReadOnlyDictionary<Guid, AgentIdentity> agents,
        TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(agents);

        int completedCount = tasks.Count(t => t.Status == TaskStatus.Completed);
        string summary = $"Coordination completed successfully. {completedCount}/{tasks.Count} tasks completed by {agents.Count} agents.";

        return new CoordinationResult(
            OriginalGoal: goal,
            Tasks: tasks,
            ParticipatingAgents: agents,
            IsSuccess: true,
            Summary: summary,
            TotalDuration: duration);
    }

    /// <summary>
    /// Creates a failed coordination result.
    /// </summary>
    /// <param name="goal">The goal that was coordinated.</param>
    /// <param name="reason">The reason for the failure.</param>
    /// <param name="tasks">The tasks that were attempted.</param>
    /// <param name="duration">The total duration before failure.</param>
    /// <returns>A <see cref="CoordinationResult"/> indicating failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public static CoordinationResult Failure(
        Goal goal,
        string reason,
        IReadOnlyList<AgentTask> tasks,
        TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(reason);
        ArgumentNullException.ThrowIfNull(tasks);

        string summary = $"Coordination failed: {reason}";

        return new CoordinationResult(
            OriginalGoal: goal,
            Tasks: tasks,
            ParticipatingAgents: ImmutableDictionary<Guid, AgentIdentity>.Empty,
            IsSuccess: false,
            Summary: summary,
            TotalDuration: duration);
    }
}