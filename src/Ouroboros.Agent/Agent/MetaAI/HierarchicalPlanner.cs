// <copyright file="HierarchicalPlanner.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Implementation of hierarchical planner for complex task decomposition.
/// Supports HTN planning, temporal constraint satisfaction, plan repair, and plan explanation.
/// </summary>
public sealed partial class HierarchicalPlanner : IHierarchicalPlanner
{
    private readonly IMetaAIPlannerOrchestrator _orchestrator;
    private readonly Ouroboros.Abstractions.Core.IChatCompletionModel _llm;

    public HierarchicalPlanner(
        IMetaAIPlannerOrchestrator orchestrator,
        Ouroboros.Abstractions.Core.IChatCompletionModel llm)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        _orchestrator = orchestrator;
        ArgumentNullException.ThrowIfNull(llm);
        _llm = llm;
    }

    /// <summary>
    /// Creates a hierarchical plan by decomposing complex tasks.
    /// </summary>
    public async Task<Result<HierarchicalPlan, string>> CreateHierarchicalPlanAsync(
        string goal,
        Dictionary<string, object>? context = null,
        HierarchicalPlanningConfig? config = null,
        CancellationToken ct = default)
    {
        config ??= new HierarchicalPlanningConfig();

        using var activity = OrchestrationTracing.StartPlanCreation(goal, config.MaxDepth);
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(goal))
        {
            stopwatch.Stop();
            OrchestrationTracing.CompletePlanCreation(activity, 0, 0, stopwatch.Elapsed, success: false);
            return Result<HierarchicalPlan, string>.Failure("Goal cannot be empty");
        }

        if (config.MaxDepth < 1)
        {
            stopwatch.Stop();
            OrchestrationTracing.CompletePlanCreation(activity, 0, 0, stopwatch.Elapsed, success: false);
            return Result<HierarchicalPlan, string>.Failure("MaxDepth must be at least 1");
        }

        Dictionary<string, object> safeContext = context ?? new Dictionary<string, object>();

        try
        {
            Result<Plan, string> topLevelResult = await _orchestrator.PlanAsync(goal, safeContext, ct).ConfigureAwait(false);

            if (!topLevelResult.IsSuccess)
            {
                stopwatch.Stop();
                OrchestrationTracing.CompletePlanCreation(activity, 0, 0, stopwatch.Elapsed, success: false);
                return Result<HierarchicalPlan, string>.Failure(topLevelResult.Error);
            }

            Plan topLevelPlan = topLevelResult.Value;
            Dictionary<string, Plan> subPlans = new Dictionary<string, Plan>();

            if (topLevelPlan.Steps.Count >= config.MinStepsForDecomposition)
            {
                await DecomposeStepsAsync(
                    topLevelPlan.Steps,
                    subPlans,
                    safeContext,
                    config,
                    currentDepth: 1,
                    ct).ConfigureAwait(false);
            }

            HierarchicalPlan hierarchicalPlan = new HierarchicalPlan(
                goal,
                topLevelPlan,
                subPlans,
                MaxDepth: config.MaxDepth,
                CreatedAt: DateTime.UtcNow);

            stopwatch.Stop();
            int totalSteps = topLevelPlan.Steps.Count + subPlans.Values.Sum(p => p.Steps.Count);
            OrchestrationTracing.CompletePlanCreation(activity, totalSteps, subPlans.Count > 0 ? 2 : 1, stopwatch.Elapsed);

            return Result<HierarchicalPlan, string>.Success(hierarchicalPlan);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            OrchestrationTracing.CompletePlanCreation(activity, 0, 0, stopwatch.Elapsed, success: false);
            return Result<HierarchicalPlan, string>.Failure($"Hierarchical planning failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes a hierarchical plan recursively.
    /// </summary>
    public async Task<Result<PlanExecutionResult, string>> ExecuteHierarchicalAsync(
        HierarchicalPlan plan,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        int totalSteps = plan.TopLevelPlan.Steps.Count + plan.SubPlans.Values.Sum(p => p.Steps.Count);
        using var activity = OrchestrationTracing.StartPlanExecution(Guid.NewGuid(), totalSteps);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            ct.ThrowIfCancellationRequested();

            Plan expandedPlan = await ExpandPlanAsync(plan, ct).ConfigureAwait(false);
            Result<PlanExecutionResult, string> executionResult = await _orchestrator.ExecuteAsync(expandedPlan, ct).ConfigureAwait(false);

            stopwatch.Stop();
            executionResult.Match(
                result =>
                {
                    int completed = result.StepResults.Count(r => r.Success);
                    int failed = result.StepResults.Count(r => !r.Success);
                    OrchestrationTracing.CompletePlanExecution(activity, completed, failed, stopwatch.Elapsed, result.Success);
                },
                _ =>
                {
                    OrchestrationTracing.CompletePlanExecution(activity, 0, 0, stopwatch.Elapsed, success: false);
                });

            return executionResult;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            OrchestrationTracing.CompletePlanExecution(activity, 0, 0, stopwatch.Elapsed, success: false);
            OrchestrationTracing.RecordError(activity, "execute_plan", ex);
            return Result<PlanExecutionResult, string>.Failure($"Hierarchical execution failed: {ex.Message}");
        }
    }

    private async Task DecomposeStepsAsync(
        List<PlanStep> steps,
        Dictionary<string, Plan> subPlans,
        Dictionary<string, object>? context,
        HierarchicalPlanningConfig config,
        int currentDepth,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (currentDepth >= config.MaxDepth)
            return;

        foreach (PlanStep step in steps)
        {
            ct.ThrowIfCancellationRequested();

            if (IsComplexStep(step, config))
            {
                string subGoal = $"Execute: {step.Action} with {JsonSerializer.Serialize(step.Parameters)}";

                Result<Plan, string> subPlanResult = await _orchestrator.PlanAsync(
                    subGoal, context ?? new Dictionary<string, object>(), ct).ConfigureAwait(false);

                if (subPlanResult.IsSuccess)
                {
                    Plan subPlan = subPlanResult.Value;
                    subPlans[step.Action] = subPlan;

                    if (subPlan.Steps.Count >= config.MinStepsForDecomposition)
                    {
                        await DecomposeStepsAsync(
                            subPlan.Steps,
                            subPlans,
                            context,
                            config,
                            currentDepth + 1,
                            ct).ConfigureAwait(false);
                    }
                }
            }
        }
    }

    private static bool IsComplexStep(PlanStep step, HierarchicalPlanningConfig config)
    {
        return step.ConfidenceScore < config.ComplexityThreshold ||
               step.Parameters.Count > 3;
    }

    private static Task<Plan> ExpandPlanAsync(HierarchicalPlan hierarchicalPlan, CancellationToken ct)
    {
        List<PlanStep> expandedSteps = new List<PlanStep>();

        foreach (PlanStep step in hierarchicalPlan.TopLevelPlan.Steps)
        {
            if (hierarchicalPlan.SubPlans.TryGetValue(step.Action, out Plan? subPlan))
            {
                expandedSteps.AddRange(subPlan.Steps);
            }
            else
            {
                expandedSteps.Add(step);
            }
        }

        Plan result = new Plan(
            hierarchicalPlan.Goal,
            expandedSteps,
            hierarchicalPlan.TopLevelPlan.ConfidenceScores,
            DateTime.UtcNow);

        return Task.FromResult(result);
    }
}
