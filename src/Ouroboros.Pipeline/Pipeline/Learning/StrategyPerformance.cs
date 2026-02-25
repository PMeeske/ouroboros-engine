namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Historical performance record for a learning strategy.
/// Used for Bayesian optimization-style adaptation.
/// </summary>
/// <param name="StrategyId">The strategy's unique identifier.</param>
/// <param name="Score">The observed performance score.</param>
/// <param name="Timestamp">When this performance was observed.</param>
/// <param name="Hyperparameters">The hyperparameter configuration at observation time.</param>
public sealed record StrategyPerformance(
    Guid StrategyId,
    double Score,
    DateTime Timestamp,
    ImmutableDictionary<string, double> Hyperparameters);