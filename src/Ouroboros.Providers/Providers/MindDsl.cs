namespace Ouroboros.Providers;

/// <summary>
/// DSL for building collective mind pipelines.
/// Provides a clean, declarative API for orchestrating AI operations.
/// </summary>
public static class MindDsl
{
    // ═══════════════════════════════════════════════════════════════════════════
    // CORE OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates an operation that generates text with thinking/streaming support.
    /// </summary>
    public static MindOperation<ThinkingResponse> Think(string prompt) =>
        MindOperation<ThinkingResponse>.FromStream(
            (mind, ct) => mind.StreamWithThinkingAsync(prompt, ct),
            (mind, ct) => mind.GenerateWithThinkingAsync(prompt, ct));

    /// <summary>
    /// Creates an operation that generates plain text.
    /// </summary>
    public static MindOperation<string> Generate(string prompt) =>
        MindOperation<string>.FromAsync((mind, ct) => mind.GenerateTextAsync(prompt, ct));

    /// <summary>
    /// Creates a racing operation that queries all pathways simultaneously.
    /// </summary>
    public static MindOperation<ThinkingResponse> Race(string prompt) =>
        MindOperation<ThinkingResponse>.FromAsync(async (mind, ct) =>
        {
            var original = mind.ThinkingMode;
            mind.ThinkingMode = CollectiveThinkingMode.Racing;
            try { return await mind.GenerateWithThinkingAsync(prompt, ct); }
            finally { mind.ThinkingMode = original; }
        });

    /// <summary>
    /// Creates an ensemble operation that gathers multiple perspectives and elects the best.
    /// </summary>
    public static MindOperation<ThinkingResponse> Ensemble(string prompt) =>
        MindOperation<ThinkingResponse>.FromAsync(async (mind, ct) =>
        {
            var original = mind.ThinkingMode;
            mind.ThinkingMode = CollectiveThinkingMode.Ensemble;
            try { return await mind.GenerateWithThinkingAsync(prompt, ct); }
            finally { mind.ThinkingMode = original; }
        });

    /// <summary>
    /// Creates a sequential operation with automatic failover.
    /// </summary>
    public static MindOperation<ThinkingResponse> Sequential(string prompt) =>
        MindOperation<ThinkingResponse>.FromAsync(async (mind, ct) =>
        {
            var original = mind.ThinkingMode;
            mind.ThinkingMode = CollectiveThinkingMode.Sequential;
            try { return await mind.GenerateWithThinkingAsync(prompt, ct); }
            finally { mind.ThinkingMode = original; }
        });

    // ═══════════════════════════════════════════════════════════════════════════
    // CONFIGURATION OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sets the master pathway for orchestration.
    /// </summary>
    public static MindOperation<VoidResult> SetMaster(string pathwayName) =>
        MindOperation<VoidResult>.FromAsync((mind, _) =>
        {
            mind.SetMaster(pathwayName);
            return Task.FromResult(VoidResult.Value);
        });

    /// <summary>
    /// Sets the election strategy.
    /// </summary>
    public static MindOperation<VoidResult> UseElection(ElectionStrategy strategy) =>
        MindOperation<VoidResult>.FromAsync((mind, _) =>
        {
            mind.ElectionStrategy = strategy;
            return Task.FromResult(VoidResult.Value);
        });

    /// <summary>
    /// Sets the thinking mode.
    /// </summary>
    public static MindOperation<VoidResult> UseMode(CollectiveThinkingMode mode) =>
        MindOperation<VoidResult>.FromAsync((mind, _) =>
        {
            mind.ThinkingMode = mode;
            return Task.FromResult(VoidResult.Value);
        });

    /// <summary>
    /// Adds a pathway to the collective.
    /// </summary>
    public static MindOperation<VoidResult> AddPathway(
        string name,
        ChatEndpointType type,
        string? model = null,
        string? endpoint = null,
        string? apiKey = null) =>
        MindOperation<VoidResult>.FromAsync((mind, _) =>
        {
            mind.AddPathway(name, type, model, endpoint, apiKey);
            return Task.FromResult(VoidResult.Value);
        });

    // ═══════════════════════════════════════════════════════════════════════════
    // DECOMPOSITION OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a decomposed operation that splits the request into sub-goals.
    /// Routes sub-goals to optimal pathways (local/cloud) based on complexity.
    /// </summary>
    public static MindOperation<ThinkingResponse> Decompose(string prompt) =>
        MindOperation<ThinkingResponse>.FromAsync(async (mind, ct) =>
        {
            var original = mind.ThinkingMode;
            mind.ThinkingMode = CollectiveThinkingMode.Decomposed;
            try { return await mind.GenerateWithThinkingAsync(prompt, ct); }
            finally { mind.ThinkingMode = original; }
        });

    /// <summary>
    /// Creates a decomposed operation with custom configuration.
    /// </summary>
    public static MindOperation<ThinkingResponse> Decompose(string prompt, DecompositionConfig config) =>
        MindOperation<ThinkingResponse>.FromAsync(async (mind, ct) =>
        {
            var originalMode = mind.ThinkingMode;
            var originalConfig = mind.DecompositionConfig;
            mind.ThinkingMode = CollectiveThinkingMode.Decomposed;
            mind.DecompositionConfig = config;
            try { return await mind.GenerateWithThinkingAsync(prompt, ct); }
            finally
            {
                mind.ThinkingMode = originalMode;
                mind.DecompositionConfig = originalConfig;
            }
        });

    /// <summary>
    /// Creates a local-first decomposed operation.
    /// Prefers local Ollama models for simple tasks, cloud for complex.
    /// </summary>
    public static MindOperation<ThinkingResponse> DecomposeLocalFirst(string prompt) =>
        Decompose(prompt, DecompositionConfig.LocalFirst);

    /// <summary>
    /// Creates a quality-first decomposed operation.
    /// Uses premium cloud models for all tasks.
    /// </summary>
    public static MindOperation<ThinkingResponse> DecomposeQualityFirst(string prompt) =>
        Decompose(prompt, DecompositionConfig.QualityFirst);

    /// <summary>
    /// Sets the decomposition configuration.
    /// </summary>
    public static MindOperation<VoidResult> UseDecomposition(DecompositionConfig config) =>
        MindOperation<VoidResult>.FromAsync((mind, _) =>
        {
            mind.ThinkingMode = CollectiveThinkingMode.Decomposed;
            mind.DecompositionConfig = config;
            return Task.FromResult(VoidResult.Value);
        });

    /// <summary>
    /// Configures a pathway's tier and specializations.
    /// </summary>
    public static MindOperation<VoidResult> ConfigurePathway(
        string pathwayName,
        PathwayTier tier,
        params SubGoalType[] specializations) =>
        MindOperation<VoidResult>.FromAsync((mind, _) =>
        {
            mind.ConfigurePathway(pathwayName, tier, specializations);
            return Task.FromResult(VoidResult.Value);
        });

    // ═══════════════════════════════════════════════════════════════════════════
    // QUERY OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets optimization suggestions from the election system.
    /// </summary>
    public static MindOperation<IReadOnlyList<OptimizationSuggestion>> GetOptimizations() =>
        MindOperation<IReadOnlyList<OptimizationSuggestion>>.FromAsync((mind, _) =>
            Task.FromResult(mind.GetOptimizationSuggestions()));

    /// <summary>
    /// Gets the current consciousness status.
    /// </summary>
    public static MindOperation<string> GetStatus() =>
        MindOperation<string>.FromAsync((mind, _) =>
            Task.FromResult(mind.GetConsciousnessStatus()));

    /// <summary>
    /// Gets all healthy pathways.
    /// </summary>
    public static MindOperation<IReadOnlyList<NeuralPathway>> GetHealthyPathways() =>
        MindOperation<IReadOnlyList<NeuralPathway>>.FromAsync((mind, _) =>
            Task.FromResult((IReadOnlyList<NeuralPathway>)mind.Pathways.Where(p => p.IsHealthy).ToList()));

    // ═══════════════════════════════════════════════════════════════════════════
    // COMBINATORS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Executes operations in sequence.
    /// </summary>
    public static MindOperation<IReadOnlyList<T>> Sequence<T>(params MindOperation<T>[] operations) =>
        MindOperation<IReadOnlyList<T>>.FromAsync(async (mind, ct) =>
        {
            var results = new List<T>();
            foreach (var op in operations)
            {
                results.Add(await op.ExecuteAsync(mind, ct));
            }
            return results;
        });

    /// <summary>
    /// Executes operations in parallel and collects results.
    /// </summary>
    public static MindOperation<IReadOnlyList<T>> Parallel<T>(params MindOperation<T>[] operations) =>
        MindOperation<IReadOnlyList<T>>.FromAsync(async (mind, ct) =>
        {
            var tasks = operations.Select(op => op.ExecuteAsync(mind, ct));
            return await Task.WhenAll(tasks);
        });

    /// <summary>
    /// Executes an operation with a fallback on failure.
    /// </summary>
    public static MindOperation<T> WithFallback<T>(MindOperation<T> primary, MindOperation<T> fallback) =>
        MindOperation<T>.FromAsync(async (mind, ct) =>
        {
            try { return await primary.ExecuteAsync(mind, ct); }
            catch { return await fallback.ExecuteAsync(mind, ct); }
        });

    /// <summary>
    /// Retries an operation with exponential backoff.
    /// </summary>
    public static MindOperation<T> WithRetry<T>(MindOperation<T> operation, int maxRetries = 3) =>
        MindOperation<T>.FromAsync(async (mind, ct) =>
        {
            int attempt = 0;
            while (true)
            {
                try { return await operation.ExecuteAsync(mind, ct); }
                catch when (++attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt)), ct);
                }
            }
        });

    /// <summary>
    /// Transforms the result of an operation.
    /// </summary>
    public static MindOperation<TResult> Map<T, TResult>(MindOperation<T> operation, Func<T, TResult> transform) =>
        operation.Select(transform);

    /// <summary>
    /// Chains operations together.
    /// </summary>
    public static MindOperation<TResult> Bind<T, TResult>(
        MindOperation<T> operation,
        Func<T, MindOperation<TResult>> next) =>
        operation.SelectMany(next);

    /// <summary>
    /// Creates a pipeline that applies a prompt template to a result.
    /// </summary>
    public static MindOperation<ThinkingResponse> ThenThink(
        MindOperation<string> operation,
        Func<string, string> promptTemplate) =>
        operation.SelectMany(result => Think(promptTemplate(result)));
}