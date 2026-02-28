// <copyright file="HierarchicalPlanner.Decomposition.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// HTN planning and temporal constraint satisfaction for HierarchicalPlanner.
/// </summary>
public sealed partial class HierarchicalPlanner
{
    /// <summary>
    /// Creates an HTN hierarchical plan by decomposing abstract tasks using task networks.
    /// </summary>
    public async Task<Result<HtnHierarchicalPlan, string>> PlanHierarchicalAsync(
        string goal,
        Dictionary<string, TaskDecomposition> taskNetwork,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            return Result<HtnHierarchicalPlan, string>.Failure("Goal cannot be empty");
        }

        if (taskNetwork == null || taskNetwork.Count == 0)
        {
            return Result<HtnHierarchicalPlan, string>.Failure("Task network cannot be empty");
        }

        try
        {
            ct.ThrowIfCancellationRequested();

            var abstractTasks = new List<AbstractTask>();
            var visitedTasks = new HashSet<string>();
            var taskQueue = new Queue<string>();

            taskQueue.Enqueue(goal);

            while (taskQueue.Count > 0)
            {
                ct.ThrowIfCancellationRequested();

                string currentTask = taskQueue.Dequeue();
                if (visitedTasks.Contains(currentTask))
                    continue;

                visitedTasks.Add(currentTask);

                var decompositions = taskNetwork
                    .Where(kvp => kvp.Value.AbstractTask == currentTask)
                    .Select(kvp => kvp.Value)
                    .ToList();

                if (decompositions.Count > 0)
                {
                    var abstractTask = new AbstractTask(
                        currentTask,
                        Preconditions: new List<string>(),
                        PossibleDecompositions: decompositions);

                    abstractTasks.Add(abstractTask);

                    foreach (var decomposition in decompositions)
                    {
                        foreach (var subTask in decomposition.SubTasks)
                        {
                            if (!visitedTasks.Contains(subTask))
                            {
                                taskQueue.Enqueue(subTask);
                            }
                        }
                    }
                }
            }

            var refinements = await GenerateRefinementsAsync(goal, abstractTasks, taskNetwork, ct);

            var htnPlan = new HtnHierarchicalPlan(goal, abstractTasks, refinements);

            return Result<HtnHierarchicalPlan, string>.Success(htnPlan);
        }
        catch (OperationCanceledException)
        {
            return Result<HtnHierarchicalPlan, string>.Failure("HTN planning was cancelled");
        }
        catch (Exception ex)
        {
            return Result<HtnHierarchicalPlan, string>.Failure($"HTN planning failed: {ex.Message}");
        }
    }

    private async Task<List<ConcretePlan>> GenerateRefinementsAsync(
        string goal,
        List<AbstractTask> abstractTasks,
        Dictionary<string, TaskDecomposition> taskNetwork,
        CancellationToken ct)
    {
        var refinements = new List<ConcretePlan>();

        foreach (var abstractTask in abstractTasks)
        {
            ct.ThrowIfCancellationRequested();

            if (abstractTask.PossibleDecompositions.Count > 0)
            {
                var selectedDecomposition = abstractTask.PossibleDecompositions[0];
                var concreteSteps = await ExpandDecompositionAsync(
                    selectedDecomposition,
                    abstractTasks,
                    taskNetwork,
                    ct);

                var concretePlan = new ConcretePlan(abstractTask.Name, concreteSteps);
                refinements.Add(concretePlan);
            }
        }

        return refinements;
    }

    private static Task<List<string>> ExpandDecompositionAsync(
        TaskDecomposition decomposition,
        List<AbstractTask> abstractTasks,
        Dictionary<string, TaskDecomposition> taskNetwork,
        CancellationToken ct)
    {
        var expandedSteps = new List<string>();

        foreach (var subTask in decomposition.SubTasks)
        {
            ct.ThrowIfCancellationRequested();

            var isAbstract = abstractTasks.Any(at => at.Name == subTask);

            if (isAbstract)
            {
                expandedSteps.Add($"[Abstract: {subTask}]");
            }
            else
            {
                expandedSteps.Add(subTask);
            }
        }

        return Task.FromResult(expandedSteps);
    }

    /// <summary>
    /// Creates a temporal plan that satisfies the given temporal constraints.
    /// </summary>
    public async Task<Result<TemporalPlan, string>> PlanWithConstraintsAsync(
        string goal,
        List<TemporalConstraint> constraints,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            return Result<TemporalPlan, string>.Failure("Goal cannot be empty");
        }

        constraints ??= new List<TemporalConstraint>();

        try
        {
            ct.ThrowIfCancellationRequested();

            var planResult = await _orchestrator.PlanAsync(goal, context: null, ct);

            if (!planResult.IsSuccess)
            {
                return Result<TemporalPlan, string>.Failure(planResult.Error);
            }

            var plan = planResult.Value;
            var taskDependencies = BuildDependencyGraph(plan.Steps, constraints);
            var scheduledTasks = await ScheduleTasksAsync(plan.Steps, constraints, taskDependencies, ct);

            if (scheduledTasks.Count == 0)
            {
                return Result<TemporalPlan, string>.Failure(
                    "Failed to create valid schedule - constraints may be unsatisfiable");
            }

            var totalDuration = scheduledTasks.Max(t => t.EndTime) - scheduledTasks.Min(t => t.StartTime);

            var temporalPlan = new TemporalPlan(goal, scheduledTasks, totalDuration);

            return Result<TemporalPlan, string>.Success(temporalPlan);
        }
        catch (OperationCanceledException)
        {
            return Result<TemporalPlan, string>.Failure("Temporal planning was cancelled");
        }
        catch (Exception ex)
        {
            return Result<TemporalPlan, string>.Failure($"Temporal planning failed: {ex.Message}");
        }
    }

    private static Dictionary<string, List<string>> BuildDependencyGraph(
        List<PlanStep> steps,
        List<TemporalConstraint> constraints)
    {
        var dependencies = new Dictionary<string, List<string>>();

        foreach (var step in steps)
        {
            dependencies[step.Action] = new List<string>();
        }

        foreach (var constraint in constraints)
        {
            switch (constraint.Relation)
            {
                case TemporalRelation.Before:
                case TemporalRelation.MustFinishBefore:
                    if (dependencies.ContainsKey(constraint.TaskB))
                    {
                        dependencies[constraint.TaskB].Add(constraint.TaskA);
                    }

                    break;

                case TemporalRelation.After:
                    if (dependencies.ContainsKey(constraint.TaskA))
                    {
                        dependencies[constraint.TaskA].Add(constraint.TaskB);
                    }

                    break;

                case TemporalRelation.During:
                case TemporalRelation.Overlaps:
                case TemporalRelation.Simultaneous:
                    break;
            }
        }

        return dependencies;
    }

    private static async Task<List<ScheduledTask>> ScheduleTasksAsync(
        List<PlanStep> steps,
        List<TemporalConstraint> constraints,
        Dictionary<string, List<string>> dependencies,
        CancellationToken ct)
    {
        var scheduledTasks = new List<ScheduledTask>();
        var completionTimes = new Dictionary<string, DateTime>();
        var currentTime = DateTime.UtcNow;
        var defaultDuration = TimeSpan.FromMinutes(5);

        var sorted = TopologicalSort(steps.Select(s => s.Action).ToList(), dependencies);

        foreach (var taskName in sorted)
        {
            ct.ThrowIfCancellationRequested();

            var step = steps.FirstOrDefault(s => s.Action == taskName);
            if (step == null)
                continue;

            var startTime = currentTime;
            var taskDeps = dependencies.ContainsKey(taskName) ? dependencies[taskName] : new List<string>();

            if (taskDeps.Count > 0)
            {
                var maxDepCompletionTime = taskDeps
                    .Where(dep => completionTimes.ContainsKey(dep))
                    .Select(dep => completionTimes[dep])
                    .DefaultIfEmpty(currentTime)
                    .Max();

                startTime = maxDepCompletionTime;
            }

            var duration = constraints
                .FirstOrDefault(c => c.TaskA == taskName && c.Duration.HasValue)
                ?.Duration ?? defaultDuration;

            var endTime = startTime + duration;

            var scheduledTask = new ScheduledTask(taskName, startTime, endTime, taskDeps);
            scheduledTasks.Add(scheduledTask);
            completionTimes[taskName] = endTime;
        }

        return scheduledTasks;
    }

    private static List<string> TopologicalSort(List<string> tasks, Dictionary<string, List<string>> dependencies)
    {
        var result = new List<string>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        void Visit(string task)
        {
            if (visited.Contains(task))
                return;

            if (visiting.Contains(task))
                return;

            visiting.Add(task);

            if (dependencies.ContainsKey(task))
            {
                foreach (var dep in dependencies[task])
                {
                    Visit(dep);
                }
            }

            visiting.Remove(task);
            visited.Add(task);
            result.Add(task);
        }

        foreach (var task in tasks)
        {
            Visit(task);
        }

        return result;
    }
}
