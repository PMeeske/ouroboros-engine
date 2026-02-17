namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Represents the internal state of an adaptive agent's learning strategy.
/// </summary>
/// <param name="CurrentStrategy">The active learning strategy.</param>
/// <param name="BaselinePerformance">The baseline performance level to compare against.</param>
/// <param name="PreviousStrategies">Stack of previous strategies for rollback support.</param>
internal sealed record AdaptiveState(
    LearningStrategy CurrentStrategy,
    double BaselinePerformance,
    ImmutableStack<LearningStrategy> PreviousStrategies)
{
    public static AdaptiveState Initial() => new(
        CurrentStrategy: LearningStrategy.Default,
        BaselinePerformance: 0.0,
        PreviousStrategies: ImmutableStack<LearningStrategy>.Empty);
}