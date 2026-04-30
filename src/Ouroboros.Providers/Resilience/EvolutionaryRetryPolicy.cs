// <copyright file="EvolutionaryRetryPolicy.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;

namespace Ouroboros.Providers.Resilience;

/// <summary>
/// An evolutionary retry policy that mutates the execution context on each retry attempt
/// using a genetic algorithm with fitness-evaluated chromosomes.
/// </summary>
/// <remarks>
/// Inspired by <c>AdaptiveParserPipeline</c>'s grammar evolution loop and
/// <c>PlanStrategyChromosome</c>'s GA-based optimization.
/// <para>
/// Each generation:
/// 1. Selects the best <see cref="IMutationStrategy{TContext}"/> based on error type and chromosome weights
/// 2. Applies the mutation with chromosome-encoded parameters
/// 3. Evaluates fitness of the chromosome based on the outcome
/// 4. Evolves the chromosome population for the next generation via crossover + mutation.
/// </para>
/// <para>
/// MeTTa atoms are generated for each mutation via <c>McpToolCallAtomConverter</c>
/// for neuro-symbolic observability and fitness tracking.
/// </para>
/// </remarks>
/// <typeparam name="TContext">The mutable execution context type.</typeparam>
public sealed class EvolutionaryRetryPolicy<TContext>
    where TContext : class
{
    private readonly IReadOnlyList<IMutationStrategy<TContext>> _strategies;
    private readonly int _maxGenerations;
    private readonly ILogger? _logger;
    private readonly ToolCallMutationChromosome _chromosome;
    private readonly ToolCallMutationFitness _fitnessFunction;
    private readonly System.Random _random;

    // Evolved chromosome — updated after each execution for learning across retries
    private ToolCallMutationChromosome _evolvedChromosome;

    /// <summary>
    /// Initializes a new instance of the <see cref="EvolutionaryRetryPolicy{TContext}"/> class.
    /// </summary>
    /// <param name="strategies">The mutation strategies, ordered by priority.</param>
    /// <param name="maxGenerations">Maximum number of mutation generations before giving up.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="chromosome">Optional initial chromosome. Uses default if not provided.</param>
    /// <param name="fitnessFunction">Optional fitness function. Uses default weights if not provided.</param>
    public EvolutionaryRetryPolicy(
        IEnumerable<IMutationStrategy<TContext>> strategies,
        int maxGenerations = 5,
        ILogger? logger = null,
        ToolCallMutationChromosome? chromosome = null,
        ToolCallMutationFitness? fitnessFunction = null)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        _strategies = strategies.OrderBy(s => s.Priority).ToList();
        _maxGenerations = maxGenerations;
        _logger = logger;
        _chromosome = chromosome ?? ToolCallMutationChromosome.CreateDefault();
        _evolvedChromosome = _chromosome;
        _fitnessFunction = fitnessFunction ?? new ToolCallMutationFitness();
        _random = new System.Random();
    }

    /// <summary>
    /// Gets the current evolved chromosome, reflecting learned parameters from past executions.
    /// </summary>
    public ToolCallMutationChromosome CurrentChromosome => _evolvedChromosome;

    /// <summary>
    /// Executes the action with evolutionary retry, mutating the context on failure.
    /// After execution, evaluates fitness and evolves the chromosome for future calls.
    /// </summary>
    /// <typeparam name="TResult">The result type of the action.</typeparam>
    /// <param name="context">The initial execution context.</param>
    /// <param name="action">The action to execute. Receives the (potentially mutated) context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the first successful execution.</returns>
    /// <exception cref="EvolutionaryRetryExhaustedException">
    /// Thrown when all generations are exhausted without success.
    /// </exception>
    public async Task<TResult> ExecuteWithEvolutionAsync<TResult>(
        TContext context,
        Func<TContext, CancellationToken, Task<TResult>> action,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(action);

        Exception? lastError = null;
        var stopwatch = Stopwatch.StartNew();
        var mutationHistory = new List<MutationHistoryEntry>();
        int generationsAttempted = 0;

        // Use a generation-specific chromosome that evolves during this execution
        var currentChromosome = _evolvedChromosome;

        for (int generation = 0; generation <= _maxGenerations; generation++)
        {
            ct.ThrowIfCancellationRequested();
            generationsAttempted = generation;

            try
            {
                _logger?.LogDebug(
                    "Evolutionary retry generation {Generation}/{Max} (chromosome fitness: {Fitness:F3})",
                    generation, _maxGenerations, currentChromosome.Fitness);

                var result = await action(context, ct).ConfigureAwait(false);

                // Success — evaluate fitness and evolve the chromosome
                stopwatch.Stop();
                EvolveChromosomeAfterExecution(
                    currentChromosome, mutationHistory, generation, succeeded: true,
                    stopwatch.Elapsed, totalCost: 0m);

                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // Intentional: evolutionary retry catches all to mutate and retry
            catch (Exception ex)
#pragma warning restore CA1031
            {
                lastError = ex;

                _logger?.LogInformation(
                    "Generation {Generation} failed: {Error}. Selecting mutation strategy.",
                    generation, ex.Message);

                if (generation >= _maxGenerations)
                {
                    break;
                }

                // Select strategy based on chromosome-weighted priorities
                IMutationStrategy<TContext>? strategy = SelectStrategyWithChromosome(
                    context, ex, currentChromosome);

                if (strategy is null)
                {
                    _logger?.LogWarning(
                        "No mutation strategy available for error type {ErrorType}. Exhausting retries.",
                        ex.GetType().Name);
                    break;
                }

                // Apply mutation
                _logger?.LogInformation(
                    "Applying mutation strategy '{Strategy}' (generation {Generation})",
                    strategy.Name, generation + 1);

                context = strategy.Mutate(context, generation + 1);

                var entry = new MutationHistoryEntry(strategy.Name, generation + 1, ex, DateTime.UtcNow);
                mutationHistory.Add(entry);

                // Raise mutation event for observability
                OnMutationApplied?.Invoke(this, new MutationAppliedEventArgs(
                    strategy.Name, generation + 1, ex));

                // Evolve the chromosome for the next generation (micro-evolution within a single execution)
                currentChromosome = currentChromosome.MutateAll(_random, mutationRate: 0.05);
            }
        }

        // All generations exhausted — evaluate fitness as failed and evolve
        stopwatch.Stop();
        EvolveChromosomeAfterExecution(
            currentChromosome, mutationHistory, generationsAttempted, succeeded: false,
            stopwatch.Elapsed, totalCost: 0m);

        throw new EvolutionaryRetryExhaustedException(
            $"All {_maxGenerations} evolutionary retry generations exhausted",
            _maxGenerations,
            lastError);
    }

    /// <summary>
    /// Raised when a mutation strategy is applied. Useful for MeTTa atom tracking
    /// and observability.
    /// </summary>
    public event EventHandler<MutationAppliedEventArgs>? OnMutationApplied;

    /// <summary>
    /// Raised when a chromosome is evolved after an execution completes.
    /// </summary>
    public event EventHandler<ChromosomeEvolvedEventArgs>? OnChromosomeEvolved;

    private IMutationStrategy<TContext>? SelectStrategyWithChromosome(
        TContext context, Exception error, ToolCallMutationChromosome chromosome)
    {
        // Get chromosome gene weights for strategy selection bias
        double formatSwitchPref = chromosome.GetGene("FormatSwitchPreference")?.Weight ?? 0.5;
        double llmVariatorWeight = chromosome.GetGene("LlmVariatorWeight")?.Weight ?? 0.2;

        // First pass: find all applicable strategies
        var applicable = _strategies
            .Where(s => s.CanMutate(context, error))
            .ToList();

        if (applicable.Count == 0)
        {
            return null;
        }

        // Score each strategy based on chromosome preferences
        var scored = applicable.Select(strategy =>
        {
            double score = 1.0 / (1.0 + strategy.Priority); // Base score from priority

            // Boost strategies that match chromosome preferences
            if (strategy.Name == "format-switch" || strategy.Name == "format-hint")
            {
                score *= 1.0 + formatSwitchPref;
            }
            else if (strategy.Name == "llm-variator")
            {
                score *= 1.0 + (llmVariatorWeight * 2.0);
            }

            return (Strategy: strategy, Score: score);
        });

        // Select the highest-scored strategy
        return scored.OrderByDescending(s => s.Score).First().Strategy;
    }

    private void EvolveChromosomeAfterExecution(
        ToolCallMutationChromosome chromosome,
        IReadOnlyList<MutationHistoryEntry> history,
        int totalGenerations,
        bool succeeded,
        TimeSpan totalLatency,
        decimal totalCost)
    {
        // Evaluate fitness
        double fitness = _fitnessFunction.Evaluate(
            chromosome, history, totalGenerations, succeeded, totalLatency, totalCost);

        var evaluatedChromosome = chromosome.WithFitness(fitness);

        // Evolve: crossover with the previous best, then mutate
        ToolCallMutationChromosome offspring;
        if (_evolvedChromosome.Fitness > 0 && evaluatedChromosome.Fitness > 0)
        {
            // Crossover with the previous generation's best
            var (child1, child2) = evaluatedChromosome.Crossover(_evolvedChromosome, _random);
            offspring = child1.Fitness >= child2.Fitness ? child1 : child2;
            offspring = offspring.MutateAll(_random, mutationRate: 0.05);
        }
        else
        {
            // No previous fitness data — just mutate the current chromosome
            offspring = evaluatedChromosome.MutateAll(_random, mutationRate: 0.1);
        }

        // Keep the fitter chromosome (elitism)
        _evolvedChromosome = offspring.WithFitness(fitness) is { Fitness: > 0 } fit
            && fit.Fitness > _evolvedChromosome.Fitness
                ? fit
                : _evolvedChromosome.Fitness > 0
                    ? _evolvedChromosome
                    : offspring.WithFitness(fitness);

        _logger?.LogDebug(
            "Chromosome evolved: fitness {OldFitness:F3} → {NewFitness:F3} ({Generations} generations, succeeded={Succeeded})",
            chromosome.Fitness, _evolvedChromosome.Fitness, totalGenerations, succeeded);

        OnChromosomeEvolved?.Invoke(this, new ChromosomeEvolvedEventArgs(
            _evolvedChromosome, fitness, totalGenerations, succeeded));
    }
}

/// <summary>
/// Event args for when a mutation strategy is applied during evolutionary retry.
/// </summary>
/// <param name="StrategyName">The mutation strategy name.</param>
/// <param name="Generation">The generation number.</param>
/// <param name="TriggeringError">The error that triggered the mutation.</param>
public sealed record MutationAppliedEventArgs(
    string StrategyName,
    int Generation,
    Exception TriggeringError);

/// <summary>
/// Event args for when a chromosome is evolved after an execution completes.
/// </summary>
/// <param name="Chromosome">The evolved chromosome.</param>
/// <param name="Fitness">The fitness score of the execution.</param>
/// <param name="GenerationsUsed">How many generations were used.</param>
/// <param name="Succeeded">Whether the execution ultimately succeeded.</param>
public sealed record ChromosomeEvolvedEventArgs(
    ToolCallMutationChromosome Chromosome,
    double Fitness,
    int GenerationsUsed,
    bool Succeeded);

/// <summary>
/// Thrown when all evolutionary retry generations are exhausted.
/// </summary>
public sealed class EvolutionaryRetryExhaustedException : Exception
{
    /// <summary>
    /// Gets the number of generations attempted.
    /// </summary>
    public int GenerationsAttempted { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EvolutionaryRetryExhaustedException"/> class.
    /// </summary>
    public EvolutionaryRetryExhaustedException(string message, int generations, Exception? inner)
        : base(message, inner)
    {
        GenerationsAttempted = generations;
    }
}
