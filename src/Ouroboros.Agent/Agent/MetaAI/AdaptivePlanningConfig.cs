namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for adaptive planning.
/// </summary>
public sealed record AdaptivePlanningConfig(
    int MaxRetries = 3,
    bool EnableAutoReplan = true,
    double FailureThreshold = 0.5);