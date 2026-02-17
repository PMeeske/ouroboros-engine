namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for persistent memory behavior.
/// </summary>
public sealed record PersistentMemoryConfig(
    int ShortTermCapacity = 100,
    int LongTermCapacity = 1000,
    double ConsolidationThreshold = 0.7,
    TimeSpan ConsolidationInterval = default,
    bool EnableForgetting = true,
    double ForgettingThreshold = 0.3);