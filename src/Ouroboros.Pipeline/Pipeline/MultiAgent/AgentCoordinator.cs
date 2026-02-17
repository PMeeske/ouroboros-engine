// <copyright file="AgentCoordinator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Orchestrates multiple agents to collaboratively execute goals through task decomposition and delegation.
/// Follows functional programming principles with immutable state and monadic error handling.
/// </summary>
public sealed class AgentCoordinator : IAgentCoordinator
{
    private AgentTeam _team;
    private readonly IMessageBus _messageBus;
    private IDelegationStrategy _delegationStrategy;
    private readonly object _teamLock = new();

    /// <inheritdoc />
    public AgentTeam Team
    {
        get
        {
            lock (_teamLock)
            {
                return _team;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentCoordinator"/> class with default round-robin strategy.
    /// </summary>
    /// <param name="team">The team of agents to coordinate.</param>
    /// <param name="messageBus">The message bus for inter-agent communication.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public AgentCoordinator(AgentTeam team, IMessageBus messageBus)
        : this(team, messageBus, DelegationStrategyFactory.RoundRobin())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentCoordinator"/> class with a specified delegation strategy.
    /// </summary>
    /// <param name="team">The team of agents to coordinate.</param>
    /// <param name="messageBus">The message bus for inter-agent communication.</param>
    /// <param name="strategy">The delegation strategy for task assignment.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public AgentCoordinator(AgentTeam team, IMessageBus messageBus, IDelegationStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(messageBus);
        ArgumentNullException.ThrowIfNull(strategy);

        _team = team;
        _messageBus = messageBus;
        _delegationStrategy = strategy;
    }

    /// <inheritdoc />
    public void SetDelegationStrategy(IDelegationStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        _delegationStrategy = strategy;
    }

    /// <inheritdoc />
    public async Task<Result<CoordinationResult, string>> ExecuteAsync(Goal goal, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(goal);

        DateTime startTime = DateTime.UtcNow;

        try
        {
            // Check if we have any agents available
            IReadOnlyList<AgentState> availableAgents = Team.GetAvailableAgents();
            if (availableAgents.Count == 0)
            {
                return Result<CoordinationResult, string>.Failure("No available agents to execute the goal.");
            }

            // Decompose the goal into tasks
            IReadOnlyList<AgentTask> tasks = DecomposeGoalToTasks(goal);

            if (tasks.Count == 0)
            {
                return Result<CoordinationResult, string>.Failure("Goal could not be decomposed into any tasks.");
            }

            // Execute all tasks
            List<AgentTask> completedTasks = new List<AgentTask>();
            Dictionary<Guid, AgentIdentity> participatingAgents = new Dictionary<Guid, AgentIdentity>();

            foreach (AgentTask task in tasks)
            {
                ct.ThrowIfCancellationRequested();

                Result<AgentTask, string> taskResult = await ExecuteTaskAsync(task, participatingAgents, ct).ConfigureAwait(false);

                if (taskResult.IsSuccess)
                {
                    completedTasks.Add(taskResult.Value);
                }
                else
                {
                    AgentTask failedTask = task.Fail(taskResult.Error);
                    completedTasks.Add(failedTask);
                }
            }

            TimeSpan duration = DateTime.UtcNow - startTime;

            // Check if all tasks completed successfully
            bool allSucceeded = completedTasks.All(t => t.Status == TaskStatus.Completed);

            if (allSucceeded)
            {
                return Result<CoordinationResult, string>.Success(
                    CoordinationResult.Success(goal, completedTasks, participatingAgents, duration));
            }
            else
            {
                int failedCount = completedTasks.Count(t => t.Status == TaskStatus.Failed);
                return Result<CoordinationResult, string>.Success(
                    CoordinationResult.Failure(goal, $"{failedCount} tasks failed during execution.", completedTasks, duration));
            }
        }
        catch (OperationCanceledException)
        {
            TimeSpan duration = DateTime.UtcNow - startTime;
            return Result<CoordinationResult, string>.Failure($"Coordination was cancelled after {duration.TotalSeconds:F2} seconds.");
        }
        catch (Exception ex)
        {
            TimeSpan duration = DateTime.UtcNow - startTime;
            return Result<CoordinationResult, string>.Failure($"Coordination failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<CoordinationResult, string>> ExecuteParallelAsync(IReadOnlyList<Goal> goals, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(goals);

        if (goals.Count == 0)
        {
            return Result<CoordinationResult, string>.Failure("No goals provided for parallel execution.");
        }

        DateTime startTime = DateTime.UtcNow;

        try
        {
            // Create tasks for each goal
            List<Task<Result<CoordinationResult, string>>> coordinationTasks =
                goals.Select(g => ExecuteAsync(g, ct)).ToList();

            // Execute all in parallel
            Result<CoordinationResult, string>[] results = await Task.WhenAll(coordinationTasks).ConfigureAwait(false);

            // Aggregate results
            List<AgentTask> allTasks = new List<AgentTask>();
            Dictionary<Guid, AgentIdentity> allAgents = new Dictionary<Guid, AgentIdentity>();
            int successCount = 0;

            foreach (Result<CoordinationResult, string> result in results)
            {
                if (result.IsSuccess)
                {
                    CoordinationResult coordination = result.Value;
                    allTasks.AddRange(coordination.Tasks);

                    foreach (KeyValuePair<Guid, AgentIdentity> kvp in coordination.ParticipatingAgents)
                    {
                        allAgents.TryAdd(kvp.Key, kvp.Value);
                    }

                    if (coordination.IsSuccess)
                    {
                        successCount++;
                    }
                }
            }

            TimeSpan duration = DateTime.UtcNow - startTime;

            // Create a composite goal for the result
            Goal compositeGoal = Goal.Atomic($"Parallel execution of {goals.Count} goals");

            bool overallSuccess = successCount == goals.Count;

            if (overallSuccess)
            {
                return Result<CoordinationResult, string>.Success(
                    CoordinationResult.Success(compositeGoal, allTasks, allAgents, duration));
            }
            else
            {
                int failedGoals = goals.Count - successCount;
                return Result<CoordinationResult, string>.Success(
                    CoordinationResult.Failure(compositeGoal, $"{failedGoals}/{goals.Count} goals failed.", allTasks, duration));
            }
        }
        catch (OperationCanceledException)
        {
            TimeSpan duration = DateTime.UtcNow - startTime;
            return Result<CoordinationResult, string>.Failure($"Parallel coordination was cancelled after {duration.TotalSeconds:F2} seconds.");
        }
        catch (Exception ex)
        {
            TimeSpan duration = DateTime.UtcNow - startTime;
            return Result<CoordinationResult, string>.Failure($"Parallel coordination failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a pipeline step that executes a goal through coordination.
    /// </summary>
    /// <returns>A <see cref="Step{TInput, TOutput}"/> that transforms a goal into a coordination result.</returns>
    public Step<Goal, Result<CoordinationResult, string>> CreateExecutionStep()
    {
        return async (Goal goal) => await ExecuteAsync(goal).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a pipeline step that executes multiple goals in parallel.
    /// </summary>
    /// <returns>A <see cref="Step{TInput, TOutput}"/> that transforms a list of goals into a coordination result.</returns>
    public Step<IReadOnlyList<Goal>, Result<CoordinationResult, string>> CreateParallelExecutionStep()
    {
        return async (IReadOnlyList<Goal> goals) => await ExecuteParallelAsync(goals).ConfigureAwait(false);
    }

    /// <summary>
    /// Decomposes a goal into individual tasks for execution.
    /// </summary>
    /// <param name="goal">The goal to decompose.</param>
    /// <returns>A list of agent tasks derived from the goal.</returns>
    private static IReadOnlyList<AgentTask> DecomposeGoalToTasks(Goal goal)
    {
        List<AgentTask> tasks = new List<AgentTask>();

        if (goal.SubGoals.Count == 0)
        {
            // Atomic goal - create a single task
            tasks.Add(AgentTask.Create(goal));
        }
        else
        {
            // Composite goal - create tasks for each sub-goal
            foreach (Goal subGoal in goal.SubGoals)
            {
                IReadOnlyList<AgentTask> subTasks = DecomposeGoalToTasks(subGoal);
                tasks.AddRange(subTasks);
            }
        }

        return tasks;
    }

    /// <summary>
    /// Executes a single task by assigning it to an appropriate agent.
    /// </summary>
    /// <param name="task">The task to execute.</param>
    /// <param name="participatingAgents">Dictionary to track participating agents.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A result containing the completed task or an error message.</returns>
    private async Task<Result<AgentTask, string>> ExecuteTaskAsync(
        AgentTask task,
        Dictionary<Guid, AgentIdentity> participatingAgents,
        CancellationToken ct)
    {
        // Get available agents
        IReadOnlyList<AgentState> availableAgents = Team.GetAvailableAgents();

        if (availableAgents.Count == 0)
        {
            return Result<AgentTask, string>.Failure("No available agents to execute the task.");
        }

        // Select an agent using the delegation strategy
        DelegationCriteria criteria = DelegationCriteria.FromGoal(task.Goal);
        DelegationResult delegationResult = _delegationStrategy.SelectAgent(criteria, Team);

        if (!delegationResult.HasMatch)
        {
            return Result<AgentTask, string>.Failure($"Delegation strategy could not select an appropriate agent: {delegationResult.Reasoning}");
        }

        Option<AgentState> selectedAgentOption = Team.GetAgent(delegationResult.SelectedAgentId!.Value);

        if (!selectedAgentOption.HasValue)
        {
            return Result<AgentTask, string>.Failure("Selected agent is no longer available.");
        }

        AgentState selectedAgent = selectedAgentOption.Value!;

        // Track participating agent
        participatingAgents.TryAdd(selectedAgent.Identity.Id, selectedAgent.Identity);

        // Update team state
        lock (_teamLock)
        {
            _team = _team.UpdateAgent(selectedAgent.Identity.Id, selectedAgent.StartTask(task.Id));
        }

        // Assign and start the task
        AgentTask assignedTask = task.AssignTo(selectedAgent.Identity.Id).Start();

        try
        {
            // Publish task assignment message
            AgentMessage taskMessage = AgentMessage.CreateRequest(
                senderId: Guid.Empty, // Coordinator
                receiverId: selectedAgent.Identity.Id,
                topic: "task.assigned",
                payload: $"Task assigned: {task.Goal.Description}");

            await _messageBus.PublishAsync(taskMessage, ct).ConfigureAwait(false);

            // Simulate task execution (in a real implementation, this would await actual work)
            await Task.Delay(TimeSpan.FromMilliseconds(100), ct).ConfigureAwait(false);

            // Complete the task
            AgentTask completedTask = assignedTask.Complete($"Task completed by agent {selectedAgent.Identity.Name}");

            // Update team state to reflect completion
            lock (_teamLock)
            {
                Option<AgentState> currentStateOption = _team.GetAgent(selectedAgent.Identity.Id);
                if (currentStateOption.HasValue)
                {
                    _team = _team.UpdateAgent(selectedAgent.Identity.Id, currentStateOption.Value!.CompleteTask());
                }
            }

            // Publish completion message
            AgentMessage completionMessage = AgentMessage.CreateNotification(
                senderId: selectedAgent.Identity.Id,
                topic: "task.completed",
                payload: $"Task completed: {task.Goal.Description}");

            await _messageBus.PublishAsync(completionMessage, ct).ConfigureAwait(false);

            return Result<AgentTask, string>.Success(completedTask);
        }
        catch (OperationCanceledException)
        {
            // Update team state to reflect cancellation
            lock (_teamLock)
            {
                Option<AgentState> currentStateOption = _team.GetAgent(selectedAgent.Identity.Id);
                if (currentStateOption.HasValue)
                {
                    _team = _team.UpdateAgent(selectedAgent.Identity.Id, currentStateOption.Value!.FailTask());
                }
            }

            throw;
        }
        catch (Exception ex)
        {
            // Update team state to reflect failure
            lock (_teamLock)
            {
                Option<AgentState> currentStateOption = _team.GetAgent(selectedAgent.Identity.Id);
                if (currentStateOption.HasValue)
                {
                    _team = _team.UpdateAgent(selectedAgent.Identity.Id, currentStateOption.Value!.FailTask());
                }
            }

            AgentTask failedTask = assignedTask.Fail(ex.Message);
            return Result<AgentTask, string>.Success(failedTask);
        }
    }
}