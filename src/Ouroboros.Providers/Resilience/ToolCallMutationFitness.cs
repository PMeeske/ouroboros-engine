// <copyright file="ToolCallMutationFitness.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.Resilience;

/// <summary>
/// Evaluates the fitness of a <see cref="ToolCallMutationChromosome"/> based on
/// historical retry outcomes, cost efficiency, and latency.
/// </summary>
/// <remarks>
/// Analogous to <c>PlanStrategyFitness</c> from <c>Ouroboros.Agent.MetaAI.Evolution</c>.
/// Uses weighted combination of: success rate (tool calls succeeded), cost (lower is better),
/// and speed (faster generations are fitter).
/// <para>
/// Mirrors <c>IFitnessFunction{TGene}</c> from <c>Ouroboros.Genetic.Abstractions</c>.
/// </para>
/// </remarks>
public sealed class ToolCallMutationFitness
{
    private readonly double _successWeight;
    private readonly double _costWeight;
    private readonly double _speedWeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolCallMutationFitness"/> class.
    /// </summary>
    /// <param name="successWeight">Weight for success rate component (default 0.5).</param>
    /// <param name="costWeight">Weight for cost efficiency component (default 0.2).</param>
    /// <param name="speedWeight">Weight for execution speed component (default 0.3).</param>
    public ToolCallMutationFitness(
        double successWeight = 0.5,
        double costWeight = 0.2,
        double speedWeight = 0.3)
    {
        double total = successWeight + costWeight + speedWeight;
        _successWeight = successWeight / total;
        _costWeight = costWeight / total;
        _speedWeight = speedWeight / total;
    }

    /// <summary>
    /// Evaluates the fitness of a chromosome based on retry history.
    /// </summary>
    /// <param name="chromosome">The chromosome to evaluate.</param>
    /// <param name="history">The mutation history entries from retry execution.</param>
    /// <param name="totalGenerations">Total generations attempted.</param>
    /// <param name="succeeded">Whether the overall retry eventually succeeded.</param>
    /// <param name="totalLatency">Total latency across all retry generations.</param>
    /// <param name="totalCost">Total cost (from LlmCostTracker) across all retry generations.</param>
    /// <returns>A fitness score between 0.0 and 1.0.</returns>
    public double Evaluate(
        ToolCallMutationChromosome chromosome,
        IReadOnlyList<MutationHistoryEntry> history,
        int totalGenerations,
        bool succeeded,
        TimeSpan totalLatency,
        decimal totalCost)
    {
        // Success component: 1.0 if succeeded on first try, decreasing with more generations
        double successScore = succeeded
            ? 1.0 / (1.0 + ((totalGenerations - 1) * 0.3)) // Penalty for needing retries
            : 0.0;

        // Cost component: normalized inverse cost (cheaper is better)
        // Uses 1/(1+cost) to bound to [0,1]
        double costScore = 1.0 / (1.0 + (double)totalCost);

        // Speed component: normalized inverse latency
        // Reference: 5 seconds per generation is "normal"
        double referenceSeconds = 5.0 * Math.Max(1, totalGenerations);
        double speedScore = 1.0 / (1.0 + (totalLatency.TotalSeconds / referenceSeconds));

        // Gene-modulated scoring: chromosome genes influence how scores are weighted
        double formatHintAggression = chromosome.GetGene("FormatHintAggression")?.Weight ?? 0.5;
        double simplificationRate = chromosome.GetGene("SimplificationRate")?.Weight ?? 0.5;

        // Higher format hint aggression helps with structured output but hurts if prompt gets too long
        double promptOverheadPenalty = formatHintAggression > 0.8 ? 0.95 : 1.0;

        // More aggressive simplification hurts if the right tool was removed
        double simplificationPenalty = !succeeded && simplificationRate > 0.7 ? 0.9 : 1.0;

        double fitness = ((_successWeight * successScore) +
                         (_costWeight * costScore) +
                         (_speedWeight * speedScore)) *
                         promptOverheadPenalty *
                         simplificationPenalty;

        return Math.Clamp(fitness, 0.0, 1.0);
    }

    /// <summary>
    /// Evaluates fitness asynchronously (for compatibility with <c>IFitnessFunction{T}</c> pattern).
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public Task<double> EvaluateAsync(
        ToolCallMutationChromosome chromosome,
        IReadOnlyList<MutationHistoryEntry> history,
        int totalGenerations,
        bool succeeded,
        TimeSpan totalLatency,
        decimal totalCost,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Evaluate(chromosome, history, totalGenerations, succeeded, totalLatency, totalCost));
    }
}
