// <copyright file="EvolutionaryRetryPolicy.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;

namespace Ouroboros.Providers.Resilience;

/// <summary>
/// An evolutionary retry policy that mutates the execution context on each retry attempt
/// instead of simply waiting and retrying the identical request.
/// </summary>
/// <remarks>
/// Inspired by <c>AdaptiveParserPipeline</c>'s grammar evolution loop:
/// generate → validate → mutate → retry. Each generation selects the best
/// available <see cref="IMutationStrategy{TContext}"/> based on the error type
/// and applies it before the next attempt.
/// <para>
/// MeTTa atoms are generated for each mutation via <c>McpToolCallAtomConverter</c>
/// for neuro-symbolic observability and fitness tracking.
/// </para>
/// </remarks>
/// <typeparam name="TContext">The mutable execution context type.</typeparam>
public sealed class EvolutionaryRetryPolicy<TContext> where TContext : class
{
    private readonly IReadOnlyList<IMutationStrategy<TContext>> _strategies;
    private readonly int _maxGenerations;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EvolutionaryRetryPolicy{TContext}"/> class.
    /// </summary>
    /// <param name="strategies">The mutation strategies, ordered by priority.</param>
    /// <param name="maxGenerations">Maximum number of mutation generations before giving up.</param>
    /// <param name="logger">Optional logger.</param>
    public EvolutionaryRetryPolicy(
        IEnumerable<IMutationStrategy<TContext>> strategies,
        int maxGenerations = 5,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        _strategies = strategies.OrderBy(s => s.Priority).ToList();
        _maxGenerations = maxGenerations;
        _logger = logger;
    }

    /// <summary>
    /// Executes the action with evolutionary retry, mutating the context on failure.
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

        for (int generation = 0; generation <= _maxGenerations; generation++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                _logger?.LogDebug(
                    "Evolutionary retry generation {Generation}/{Max}",
                    generation, _maxGenerations);

                return await action(context, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;

                _logger?.LogInformation(
                    "Generation {Generation} failed: {Error}. Selecting mutation strategy.",
                    generation, ex.Message);

                if (generation >= _maxGenerations)
                {
                    break;
                }

                // Select the best mutation strategy for this error
                IMutationStrategy<TContext>? strategy = SelectStrategy(context, ex);
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

                // Raise mutation event for observability
                OnMutationApplied?.Invoke(this, new MutationAppliedEventArgs(
                    strategy.Name, generation + 1, ex));
            }
        }

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

    private IMutationStrategy<TContext>? SelectStrategy(TContext context, Exception error)
    {
        return _strategies.FirstOrDefault(s => s.CanMutate(context, error));
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
