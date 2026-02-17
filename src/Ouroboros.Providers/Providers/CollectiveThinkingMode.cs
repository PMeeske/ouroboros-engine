namespace Ouroboros.Providers;

/// <summary>
/// Thinking mode for the collective mind.
/// </summary>
public enum CollectiveThinkingMode
{
    /// <summary>First successful response wins (fastest).</summary>
    Racing,
    /// <summary>Round-robin with failover (balanced).</summary>
    Sequential,
    /// <summary>Query multiple providers and synthesize (highest quality).</summary>
    Ensemble,
    /// <summary>Adaptive selection based on pathway health and query complexity.</summary>
    Adaptive,
    /// <summary>Decompose request into sub-goals and route to optimal pathways.</summary>
    Decomposed
}