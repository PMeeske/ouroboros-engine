namespace Ouroboros.Agent;

/// <summary>
/// Fluent builder for creating composite orchestrators.
/// Provides a chainable API for building complex orchestration pipelines.
/// </summary>
public sealed class OrchestratorComposer
{
    /// <summary>
    /// Starts a composition chain with the given orchestrator.
    /// </summary>
    public static IComposableOrchestrator<TInput, TOutput> Start<TInput, TOutput>(
        IOrchestrator<TInput, TOutput> orchestrator)
    {
        return CompositeOrchestrator<TInput, TOutput>.From(orchestrator);
    }

    /// <summary>
    /// Starts a composition chain with a function.
    /// </summary>
    public static IComposableOrchestrator<TInput, TOutput> StartWith<TInput, TOutput>(
        string name,
        Func<TInput, Task<TOutput>> func,
        OrchestratorConfig? config = null)
    {
        return CompositeOrchestrator<TInput, TOutput>.FromFunc(
            name,
            (input, _) => func(input),
            config);
    }

    /// <summary>
    /// Creates a parallel orchestrator that executes multiple orchestrators concurrently.
    /// </summary>
    /// <param name="maxConcurrency">Maximum number of orchestrators to execute concurrently. Use -1 for unlimited.</param>
    /// <param name="orchestrators">The orchestrators to execute in parallel.</param>
    public static IComposableOrchestrator<TInput, TOutput[]> Parallel<TInput, TOutput>(
        int maxConcurrency,
        IOrchestrator<TInput, TOutput>[] orchestrators)
    {
        ArgumentNullException.ThrowIfNull(orchestrators);
        if (orchestrators.Length == 0)
        {
            throw new ArgumentException("At least one orchestrator is required", nameof(orchestrators));
        }

        return CompositeOrchestrator<TInput, TOutput[]>.FromFunc(
            "parallel_orchestrator",
            async (input, context) =>
            {
                var semaphore = maxConcurrency > 0 
                    ? new SemaphoreSlim(maxConcurrency, maxConcurrency) 
                    : null;

                try
                {
                    var tasks = orchestrators.Select(async o =>
                    {
                        if (semaphore != null)
                        {
                            await semaphore.WaitAsync(context.CancellationToken).ConfigureAwait(false);
                        }
                        try
                        {
                            return await o.ExecuteAsync(input, context).ConfigureAwait(false);
                        }
                        finally
                        {
                            semaphore?.Release();
                        }
                    }).ToArray();

                    var results = await Task.WhenAll(tasks).ConfigureAwait(false);

                    // Check for failures
                    var failures = results.Where(r => !r.Success).ToList();
                    if (failures.Any())
                    {
                        var errors = string.Join("; ", failures.Select(f => f.ErrorMessage));
                        throw new InvalidOperationException($"Parallel orchestration had failures: {errors}");
                    }

                    return results.Select(r => r.Output!).ToArray();
                }
                finally
                {
                    semaphore?.Dispose();
                }
            });
    }

    /// <summary>
    /// Creates a parallel orchestrator that executes multiple orchestrators concurrently without limits.
    /// </summary>
    /// <param name="orchestrators">The orchestrators to execute in parallel.</param>
    public static IComposableOrchestrator<TInput, TOutput[]> Parallel<TInput, TOutput>(
        params IOrchestrator<TInput, TOutput>[] orchestrators)
    {
        return Parallel(-1, orchestrators);
    }

    /// <summary>
    /// Creates a fallback orchestrator that tries the primary orchestrator first,
    /// then falls back to the secondary if the primary fails.
    /// </summary>
    public static IComposableOrchestrator<TInput, TOutput> WithFallback<TInput, TOutput>(
        IOrchestrator<TInput, TOutput> primary,
        IOrchestrator<TInput, TOutput> fallback)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(fallback);

        return CompositeOrchestrator<TInput, TOutput>.FromFunc(
            "fallback_orchestrator",
            async (input, context) =>
            {
                var primaryResult = await primary.ExecuteAsync(input, context);
                if (primaryResult.Success)
                {
                    return primaryResult.Output!;
                }

                // Try fallback
                var fallbackResult = await fallback.ExecuteAsync(input, context);
                if (!fallbackResult.Success)
                {
                    throw new InvalidOperationException(
                        $"Both primary and fallback failed. Primary: {primaryResult.ErrorMessage}, Fallback: {fallbackResult.ErrorMessage}");
                }

                return fallbackResult.Output!;
            });
    }

    /// <summary>
    /// Creates a conditional orchestrator that routes based on a predicate.
    /// </summary>
    public static IComposableOrchestrator<TInput, TOutput> Conditional<TInput, TOutput>(
        Func<TInput, bool> condition,
        IOrchestrator<TInput, TOutput> whenTrue,
        IOrchestrator<TInput, TOutput> whenFalse)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(whenTrue);
        ArgumentNullException.ThrowIfNull(whenFalse);

        return CompositeOrchestrator<TInput, TOutput>.FromFunc(
            "conditional_orchestrator",
            async (input, context) =>
            {
                var selectedOrchestrator = condition(input) ? whenTrue : whenFalse;
                var result = await selectedOrchestrator.ExecuteAsync(input, context);
                if (!result.Success)
                {
                    throw new InvalidOperationException(result.ErrorMessage ?? "Conditional orchestration failed");
                }
                return result.Output!;
            });
    }

    /// <summary>
    /// Creates a retry orchestrator that retries on failure.
    /// </summary>
    public static IComposableOrchestrator<TInput, TOutput> WithRetry<TInput, TOutput>(
        IOrchestrator<TInput, TOutput> orchestrator,
        int maxRetries = 3,
        TimeSpan? delay = null)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);

        var retryDelay = delay ?? TimeSpan.FromMilliseconds(100);

        return CompositeOrchestrator<TInput, TOutput>.FromFunc(
            "retry_orchestrator",
            async (input, context) =>
            {
                var attempt = 0;
                while (true)
                {
                    attempt++;
                    var result = await orchestrator.ExecuteAsync(input, context);
                    
                    if (result.Success)
                    {
                        return result.Output!;
                    }

                    if (attempt >= maxRetries)
                    {
                        throw new InvalidOperationException(
                            $"Operation failed after {attempt} attempts: {result.ErrorMessage}");
                    }

                    await Task.Delay(retryDelay, context.CancellationToken);
                }
            });
    }
}