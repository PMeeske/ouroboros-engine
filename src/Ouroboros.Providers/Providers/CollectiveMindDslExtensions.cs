namespace Ouroboros.Providers;

/// <summary>
/// Extensions for CollectiveMind to enable DSL usage.
/// </summary>
public static class CollectiveMindDslExtensions
{
    /// <summary>
    /// Executes a DSL operation on the collective mind.
    /// </summary>
    public static Task<T> RunAsync<T>(this CollectiveMind mind, MindOperation<T> operation, CancellationToken ct = default) =>
        operation.ExecuteAsync(mind, ct);

    /// <summary>
    /// Creates a streaming pipeline.
    /// </summary>
    public static StreamingPipeline Stream(this CollectiveMind mind) =>
        new(mind);

    /// <summary>
    /// Executes a DSL pipeline: config -> operation -> result.
    /// </summary>
    public static async Task<TResult> PipelineAsync<TResult>(
        this CollectiveMind mind,
        MindOperation<VoidResult> config,
        MindOperation<TResult> operation,
        CancellationToken ct = default)
    {
        await config.ExecuteAsync(mind, ct);
        return await operation.ExecuteAsync(mind, ct);
    }

    /// <summary>
    /// Fluent: Sets master and returns the mind.
    /// </summary>
    public static CollectiveMind WithMaster(this CollectiveMind mind, string pathwayName)
    {
        mind.SetMaster(pathwayName);
        return mind;
    }

    /// <summary>
    /// Fluent: Sets election strategy and returns the mind.
    /// </summary>
    public static CollectiveMind WithElection(this CollectiveMind mind, ElectionStrategy strategy)
    {
        mind.ElectionStrategy = strategy;
        return mind;
    }

    /// <summary>
    /// Fluent: Sets thinking mode and returns the mind.
    /// </summary>
    public static CollectiveMind WithMode(this CollectiveMind mind, CollectiveThinkingMode mode)
    {
        mind.ThinkingMode = mode;
        return mind;
    }

    /// <summary>
    /// Fluent: Enables decomposed mode for goal splitting.
    /// </summary>
    public static CollectiveMind WithDecomposition(this CollectiveMind mind, DecompositionConfig? config = null)
    {
        mind.ThinkingMode = CollectiveThinkingMode.Decomposed;
        if (config != null)
            mind.DecompositionConfig = config;
        return mind;
    }

    /// <summary>
    /// Fluent: Configures decomposition to prefer local models.
    /// </summary>
    public static CollectiveMind WithLocalFirst(this CollectiveMind mind)
    {
        mind.ThinkingMode = CollectiveThinkingMode.Decomposed;
        mind.DecompositionConfig = DecompositionConfig.LocalFirst;
        return mind;
    }

    /// <summary>
    /// Fluent: Configures decomposition to prefer quality (cloud premium).
    /// </summary>
    public static CollectiveMind WithQualityFirst(this CollectiveMind mind)
    {
        mind.ThinkingMode = CollectiveThinkingMode.Decomposed;
        mind.DecompositionConfig = DecompositionConfig.QualityFirst;
        return mind;
    }

    /// <summary>
    /// Fluent: Configures a pathway's tier and specializations.
    /// </summary>
    public static CollectiveMind WithPathwayConfig(
        this CollectiveMind mind,
        string pathwayName,
        PathwayTier tier,
        params SubGoalType[] specializations)
    {
        mind.ConfigurePathway(pathwayName, tier, specializations);
        return mind;
    }

    /// <summary>
    /// Creates a DSL expression from a string (simple prompt shorthand).
    /// </summary>
    public static MindOperation<ThinkingResponse> Ask(this CollectiveMind _, string prompt) =>
        MindDsl.Think(prompt);
}