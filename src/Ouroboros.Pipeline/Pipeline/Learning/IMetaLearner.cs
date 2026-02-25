namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Defines the interface for meta-learning strategy evaluation and adaptation.
/// Implements the outer loop of learning-to-learn optimization.
/// </summary>
public interface IMetaLearner
{
    /// <summary>
    /// Evaluates the effectiveness of a learning strategy based on metrics.
    /// </summary>
    /// <param name="strategy">The strategy to evaluate.</param>
    /// <param name="metrics">The learning metrics observed under this strategy.</param>
    /// <returns>A score representing strategy effectiveness (higher is better).</returns>
    double EvaluateStrategy(LearningStrategy strategy, LearningMetrics metrics);

    /// <summary>
    /// Adapts a learning strategy based on observed performance metrics.
    /// Uses Bayesian optimization principles to suggest improved hyperparameters.
    /// </summary>
    /// <param name="current">The current learning strategy.</param>
    /// <param name="metrics">The metrics observed under the current strategy.</param>
    /// <returns>An adapted strategy with optimized hyperparameters, or an error.</returns>
    Result<LearningStrategy, string> AdaptStrategy(LearningStrategy current, LearningMetrics metrics);

    /// <summary>
    /// Selects the best strategy from a collection based on metrics.
    /// </summary>
    /// <param name="strategies">The candidate strategies to evaluate.</param>
    /// <param name="metrics">The current learning metrics for context.</param>
    /// <returns>The best strategy, or an error if no valid strategy exists.</returns>
    Result<LearningStrategy, string> SelectBestStrategy(
        IEnumerable<LearningStrategy> strategies,
        LearningMetrics metrics);
}