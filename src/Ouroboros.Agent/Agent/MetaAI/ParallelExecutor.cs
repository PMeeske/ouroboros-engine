#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Parallel Executor - Execute independent steps concurrently
// ==========================================================

using System.Collections.Concurrent;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Executes plan steps in parallel when they are independent.
/// </summary>
public sealed class ParallelExecutor
{
    private readonly ISafetyGuard _safety;
    private readonly Func<PlanStep, CancellationToken, Task<StepResult>> _executeStep;

    public ParallelExecutor(
        ISafetyGuard safety,
        Func<PlanStep, CancellationToken, Task<StepResult>> executeStep)
    {
        _safety = safety ?? throw new ArgumentNullException(nameof(safety));
        _executeStep = executeStep ?? throw new ArgumentNullException(nameof(executeStep));
    }

    /// <summary>
    /// Executes a plan with parallel execution of independent steps.
    /// </summary>
    public async Task<(List<StepResult> results, bool success, string output)> ExecuteParallelAsync(
        Plan plan,
        CancellationToken ct = default)
    {
        StepDependencyGraph dependencyGraph = new StepDependencyGraph(plan.Steps);
        List<List<int>> parallelGroups = dependencyGraph.GetParallelGroups();

        ConcurrentDictionary<int, StepResult> allResults = new ConcurrentDictionary<int, StepResult>();
        bool overallSuccess = true;
        ConcurrentBag<string> outputs = new ConcurrentBag<string>();

        foreach (List<int> group in parallelGroups)
        {
            if (ct.IsCancellationRequested)
                break;

            // Execute all steps in this group in parallel
            IEnumerable<Task<StepResult>> groupTasks = group.Select(async stepIndex =>
            {
                PlanStep step = plan.Steps[stepIndex];
                PlanStep sandboxedStep = _safety.SandboxStep(step);
                StepResult result = await _executeStep(sandboxedStep, ct);

                allResults[stepIndex] = result;

                if (!result.Success)
                {
                    overallSuccess = false;
                }

                outputs.Add(result.Output);

                return result;
            });

            await Task.WhenAll(groupTasks);
        }

        // Order results by step index
        List<StepResult> orderedResults = Enumerable.Range(0, plan.Steps.Count)
            .Select(i => allResults.TryGetValue(i, out StepResult? result) ? result : null)
            .Where(r => r != null)
            .Select(r => r!)
            .ToList();

        string finalOutput = string.Join("\n", outputs.Where(o => !string.IsNullOrEmpty(o)));

        return (orderedResults, overallSuccess, finalOutput);
    }

    /// <summary>
    /// Estimates the speedup from parallel execution.
    /// </summary>
    public double EstimateSpeedup(Plan plan)
    {
        StepDependencyGraph dependencyGraph = new StepDependencyGraph(plan.Steps);
        List<List<int>> parallelGroups = dependencyGraph.GetParallelGroups();

        // Speedup = total steps / number of parallel groups
        int sequentialSteps = plan.Steps.Count;
        int parallelSteps = parallelGroups.Count;

        return parallelSteps > 0 ? (double)sequentialSteps / parallelSteps : 1.0;
    }
}
