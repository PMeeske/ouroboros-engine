namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for capability registry behavior.
/// </summary>
public sealed record CapabilityRegistryConfig(
    double MinSuccessRateThreshold = 0.6,
    int MinUsageCountForReliability = 5,
    TimeSpan CapabilityExpirationTime = default);