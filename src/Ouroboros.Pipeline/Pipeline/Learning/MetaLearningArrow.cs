namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Provides Kleisli arrow factories for meta-learning pipeline integration.
/// Enables composable meta-learning workflows using Step abstractions.
/// </summary>
public static class MetaLearningArrow
{
    /// <summary>
    /// Creates an arrow that evaluates a strategy's effectiveness.
    /// </summary>
    /// <param name="metaLearner">The meta-learner to use for evaluation.</param>
    /// <returns>A step that scores a strategy-metrics pair.</returns>
    public static Step<(LearningStrategy Strategy, LearningMetrics Metrics), double> EvaluateArrow(
        IMetaLearner metaLearner)
    {
        ArgumentNullException.ThrowIfNull(metaLearner);
        return input => Task.FromResult(metaLearner.EvaluateStrategy(input.Strategy, input.Metrics));
    }

    /// <summary>
    /// Creates an arrow that adapts a learning strategy based on metrics.
    /// This is the core meta-learning operation for hyperparameter optimization.
    /// </summary>
    /// <param name="metaLearner">The meta-learner to use for adaptation.</param>
    /// <returns>A step that transforms strategy-metrics pairs into adapted strategies.</returns>
    public static Step<(LearningStrategy Strategy, LearningMetrics Metrics), Result<LearningStrategy, string>> AdaptArrow(
        IMetaLearner metaLearner)
    {
        ArgumentNullException.ThrowIfNull(metaLearner);
        return input => Task.FromResult(metaLearner.AdaptStrategy(input.Strategy, input.Metrics));
    }

    /// <summary>
    /// Creates an arrow that selects the best strategy from candidates.
    /// </summary>
    /// <param name="metaLearner">The meta-learner to use for selection.</param>
    /// <returns>A step that selects the best strategy given candidates and metrics.</returns>
    public static Step<(IEnumerable<LearningStrategy> Strategies, LearningMetrics Metrics), Result<LearningStrategy, string>> SelectArrow(
        IMetaLearner metaLearner)
    {
        ArgumentNullException.ThrowIfNull(metaLearner);
        return input => Task.FromResult(metaLearner.SelectBestStrategy(input.Strategies, input.Metrics));
    }

    /// <summary>
    /// Creates a composed arrow that evaluates, adapts, and validates a strategy.
    /// Implements the full meta-learning cycle as a single composable step.
    /// </summary>
    /// <param name="metaLearner">The meta-learner to use.</param>
    /// <returns>A step implementing the complete adaptation cycle.</returns>
    public static Step<(LearningStrategy Strategy, LearningMetrics Metrics), Result<LearningStrategy, string>> AdaptAndValidateArrow(
        IMetaLearner metaLearner)
    {
        ArgumentNullException.ThrowIfNull(metaLearner);

        return async input =>
        {
            var adaptResult = metaLearner.AdaptStrategy(input.Strategy, input.Metrics);
            if (adaptResult.IsFailure)
            {
                return adaptResult;
            }

            var adapted = adaptResult.Value;
            var validationResult = adapted.Validate();

            return validationResult.IsSuccess
                ? Result<LearningStrategy, string>.Success(adapted)
                : Result<LearningStrategy, string>.Failure($"Adapted strategy failed validation: {validationResult.Error}");
        };
    }

    /// <summary>
    /// Creates an arrow that updates metrics with a new reward observation.
    /// </summary>
    /// <returns>A step that updates learning metrics with a new reward.</returns>
    public static Step<(LearningMetrics Metrics, double Reward), LearningMetrics> UpdateMetricsArrow()
        => input => Task.FromResult(input.Metrics.WithNewReward(input.Reward));

    /// <summary>
    /// Creates an arrow that computes a performance score from metrics.
    /// </summary>
    /// <returns>A step that computes a normalized performance score.</returns>
    public static Step<LearningMetrics, double> PerformanceScoreArrow()
        => metrics => Task.FromResult(metrics.ComputePerformanceScore());

    /// <summary>
    /// Creates an arrow for iterative meta-learning over multiple episodes.
    /// Applies adaptation after each episode's metrics update.
    /// </summary>
    /// <param name="metaLearner">The meta-learner to use.</param>
    /// <param name="maxIterations">Maximum number of adaptation iterations.</param>
    /// <param name="convergenceThreshold">Stop when improvement falls below this threshold.</param>
    /// <returns>A step that performs iterative meta-learning.</returns>
    public static Step<(LearningStrategy Initial, IEnumerable<double> Rewards), Result<(LearningStrategy Final, LearningMetrics Metrics), string>> IterativeAdaptArrow(
        IMetaLearner metaLearner,
        int maxIterations = 10,
        double convergenceThreshold = 0.001)
    {
        ArgumentNullException.ThrowIfNull(metaLearner);

        return async input =>
        {
            var strategy = input.Initial;
            var metrics = LearningMetrics.FromRewards(input.Rewards);
            var previousScore = metrics.ComputePerformanceScore();

            for (int i = 0; i < maxIterations; i++)
            {
                var adaptResult = metaLearner.AdaptStrategy(strategy, metrics);
                if (adaptResult.IsFailure)
                {
                    return Result<(LearningStrategy Final, LearningMetrics Metrics), string>.Failure(adaptResult.Error);
                }

                strategy = adaptResult.Value;
                var currentScore = metaLearner.EvaluateStrategy(strategy, metrics);

                // Check convergence
                if (Math.Abs(currentScore - previousScore) < convergenceThreshold)
                {
                    break;
                }

                previousScore = currentScore;
            }

            return Result<(LearningStrategy Final, LearningMetrics Metrics), string>.Success((strategy, metrics));
        };
    }
}