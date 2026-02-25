namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for hypothesis generation and testing.
/// </summary>
public sealed record HypothesisEngineConfig(
    double MinConfidenceForTesting = 0.3,
    int MaxHypothesesPerDomain = 10,
    bool EnableAbductiveReasoning = true,
    bool AutoGenerateCounterExamples = true);