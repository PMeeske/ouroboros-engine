// <copyright file="IMutationStrategy.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.Resilience;

/// <summary>
/// Strategy pattern interface for evolutionary retry mutations.
/// Each strategy mutates the execution context before the next retry attempt,
/// enabling the system to adapt rather than repeat the same failing request.
/// </summary>
/// <typeparam name="TContext">The mutable execution context type.</typeparam>
public interface IMutationStrategy<TContext>
    where TContext : class
{
    /// <summary>
    /// Gets the human-readable name of this mutation strategy.
    /// Used for MeTTa atom tracking and logging.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the priority of this strategy. Lower values are tried first.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Determines whether this strategy can meaningfully mutate the context
    /// given the last error that occurred.
    /// </summary>
    /// <param name="context">The current execution context.</param>
    /// <param name="lastError">The exception from the last attempt.</param>
    /// <returns><c>true</c> if this strategy has a mutation to offer.</returns>
    bool CanMutate(TContext context, Exception lastError);

    /// <summary>
    /// Mutates the context for the next retry attempt.
    /// </summary>
    /// <param name="context">The current execution context (modified in place or cloned).</param>
    /// <param name="generation">The generation/iteration number (0-based).</param>
    /// <returns>The mutated context.</returns>
    TContext Mutate(TContext context, int generation);
}
