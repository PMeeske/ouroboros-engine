// <copyright file="HierarchicalPlanner.Execution.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Plan repair strategies and plan explanation for HierarchicalPlanner.
/// </summary>
public sealed partial class HierarchicalPlanner
{
    /// <summary>
    /// Repairs a broken plan using the specified repair strategy.
    /// </summary>
    public async Task<Result<Plan, string>> RepairPlanAsync(
        Plan brokenPlan,
        ExecutionTrace trace,
        RepairStrategy strategy,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(brokenPlan);
        ArgumentNullException.ThrowIfNull(trace);

        try
        {
            ct.ThrowIfCancellationRequested();

            return strategy switch
            {
                RepairStrategy.Replan => await ReplanStrategyAsync(brokenPlan, trace, ct),
                RepairStrategy.Patch => await PatchStrategyAsync(brokenPlan, trace, ct),
                RepairStrategy.CaseBased => await CaseBasedStrategyAsync(brokenPlan, trace, ct),
                RepairStrategy.Backtrack => await BacktrackStrategyAsync(brokenPlan, trace, ct),
                _ => Result<Plan, string>.Failure($"Unknown repair strategy: {strategy}")
            };
        }
        catch (OperationCanceledException)
        {
            return Result<Plan, string>.Failure("Plan repair was cancelled");
        }
        catch (Exception ex)
        {
            return Result<Plan, string>.Failure($"Plan repair failed: {ex.Message}");
        }
    }

    private async Task<Result<Plan, string>> ReplanStrategyAsync(
        Plan brokenPlan,
        ExecutionTrace trace,
        CancellationToken ct)
    {
        var context = new Dictionary<string, object>
        {
            ["executed_steps"] = trace.Steps.Where(s => s.Success).Select(s => s.StepName).ToList(),
            ["failure_reason"] = trace.FailureReason,
            ["failed_step"] = trace.FailedAtIndex < trace.Steps.Count
                ? trace.Steps[trace.FailedAtIndex].StepName
                : "unknown"
        };

        var replanResult = await _orchestrator.PlanAsync(brokenPlan.Goal, context, ct);

        if (!replanResult.IsSuccess)
        {
            return Result<Plan, string>.Failure($"Replanning failed: {replanResult.Error}");
        }

        return Result<Plan, string>.Success(replanResult.Value);
    }

    private static Task<Result<Plan, string>> PatchStrategyAsync(
        Plan brokenPlan,
        ExecutionTrace trace,
        CancellationToken ct)
    {
        var newSteps = new List<PlanStep>();

        for (int i = 0; i < trace.FailedAtIndex && i < brokenPlan.Steps.Count; i++)
        {
            newSteps.Add(brokenPlan.Steps[i]);
        }

        if (trace.FailedAtIndex < brokenPlan.Steps.Count)
        {
            var failedStep = brokenPlan.Steps[trace.FailedAtIndex];

            var patchedStep = new PlanStep(
                failedStep.Action + "_alt",
                new Dictionary<string, object>(failedStep.Parameters) { ["retry"] = true },
                failedStep.ExpectedOutcome,
                failedStep.ConfidenceScore * 0.8);

            newSteps.Add(patchedStep);
        }

        for (int i = trace.FailedAtIndex + 1; i < brokenPlan.Steps.Count; i++)
        {
            newSteps.Add(brokenPlan.Steps[i]);
        }

        var patchedPlan = new Plan(
            brokenPlan.Goal,
            newSteps,
            brokenPlan.ConfidenceScores,
            DateTime.UtcNow);

        return Task.FromResult(Result<Plan, string>.Success(patchedPlan));
    }

    private async Task<Result<Plan, string>> CaseBasedStrategyAsync(
        Plan brokenPlan,
        ExecutionTrace trace,
        CancellationToken ct)
    {
        var hasRecentFailure = trace.Steps.Count(s => !s.Success) > 1;

        if (hasRecentFailure)
        {
            return await ReplanStrategyAsync(brokenPlan, trace, ct);
        }
        else
        {
            return await PatchStrategyAsync(brokenPlan, trace, ct);
        }
    }

    private async Task<Result<Plan, string>> BacktrackStrategyAsync(
        Plan brokenPlan,
        ExecutionTrace trace,
        CancellationToken ct)
    {
        var newSteps = new List<PlanStep>();

        var checkpointIndex = Math.Max(0, trace.FailedAtIndex - 3);

        for (int i = 0; i < checkpointIndex && i < brokenPlan.Steps.Count; i++)
        {
            newSteps.Add(brokenPlan.Steps[i]);
        }

        var remainingGoal = $"Complete: {brokenPlan.Goal} (from step {checkpointIndex})";
        var context = new Dictionary<string, object>
        {
            ["checkpoint_index"] = checkpointIndex,
            ["previous_failure"] = trace.FailureReason
        };

        var alternativeResult = await _orchestrator.PlanAsync(remainingGoal, context, ct);

        if (alternativeResult.IsSuccess)
        {
            newSteps.AddRange(alternativeResult.Value.Steps);
        }
        else
        {
            for (int i = checkpointIndex; i < brokenPlan.Steps.Count; i++)
            {
                newSteps.Add(brokenPlan.Steps[i]);
            }
        }

        var repairedPlan = new Plan(
            brokenPlan.Goal,
            newSteps,
            brokenPlan.ConfidenceScores,
            DateTime.UtcNow);

        return Result<Plan, string>.Success(repairedPlan);
    }

    /// <summary>
    /// Generates an explanation of a plan at the specified level of detail.
    /// </summary>
    public async Task<Result<string, string>> ExplainPlanAsync(
        Plan plan,
        ExplanationLevel level,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        try
        {
            ct.ThrowIfCancellationRequested();

            var explanation = level switch
            {
                ExplanationLevel.Brief => await GenerateBriefExplanationAsync(plan, ct),
                ExplanationLevel.Detailed => await GenerateDetailedExplanationAsync(plan, ct),
                ExplanationLevel.Causal => await GenerateCausalExplanationAsync(plan, ct),
                ExplanationLevel.Counterfactual => await GenerateCounterfactualExplanationAsync(plan, ct),
                _ => $"Unknown explanation level: {level}"
            };

            return Result<string, string>.Success(explanation);
        }
        catch (OperationCanceledException)
        {
            return Result<string, string>.Failure("Plan explanation was cancelled");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Plan explanation failed: {ex.Message}");
        }
    }

    private static Task<string> GenerateBriefExplanationAsync(Plan plan, CancellationToken ct)
    {
        var summary = $"Plan to achieve '{plan.Goal}' in {plan.Steps.Count} steps";
        return Task.FromResult(summary);
    }

    private static Task<string> GenerateDetailedExplanationAsync(Plan plan, CancellationToken ct)
    {
        var explanation = new StringBuilder();
        explanation.AppendLine($"Goal: {plan.Goal}");
        explanation.AppendLine($"Total Steps: {plan.Steps.Count}");
        explanation.AppendLine();
        explanation.AppendLine("Step-by-Step Plan:");

        for (int i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            explanation.AppendLine($"{i + 1}. {step.Action}");
            explanation.AppendLine($"   Parameters: {string.Join(", ", step.Parameters.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
            explanation.AppendLine($"   Expected Outcome: {step.ExpectedOutcome}");
            explanation.AppendLine($"   Confidence: {step.ConfidenceScore:P0}");
            explanation.AppendLine();
        }

        return Task.FromResult(explanation.ToString());
    }

    private static Task<string> GenerateCausalExplanationAsync(Plan plan, CancellationToken ct)
    {
        var explanation = new StringBuilder();
        explanation.AppendLine($"Causal Explanation for: {plan.Goal}");
        explanation.AppendLine();

        for (int i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            explanation.AppendLine($"Step {i + 1}: {step.Action}");

            if (i == 0)
            {
                explanation.AppendLine($"   Why: This is the initial step required to begin working toward '{plan.Goal}'");
            }
            else
            {
                var previousStep = plan.Steps[i - 1];
                explanation.AppendLine($"   Why: This step builds on '{previousStep.Action}' to progress toward the goal");
            }

            explanation.AppendLine($"   Contribution: {step.ExpectedOutcome}");
            explanation.AppendLine();
        }

        return Task.FromResult(explanation.ToString());
    }

    private static Task<string> GenerateCounterfactualExplanationAsync(Plan plan, CancellationToken ct)
    {
        var explanation = new StringBuilder();
        explanation.AppendLine($"Counterfactual Analysis for: {plan.Goal}");
        explanation.AppendLine();

        for (int i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            explanation.AppendLine($"Step {i + 1}: {step.Action}");

            if (i == plan.Steps.Count - 1)
            {
                explanation.AppendLine($"   Without this step: The goal '{plan.Goal}' would not be achieved");
            }
            else
            {
                var nextStep = plan.Steps[i + 1];
                explanation.AppendLine($"   Without this step: Cannot proceed to '{nextStep.Action}' as preconditions wouldn't be met");
            }

            explanation.AppendLine($"   Critical because: {step.ExpectedOutcome}");
            explanation.AppendLine();
        }

        return Task.FromResult(explanation.ToString());
    }
}
