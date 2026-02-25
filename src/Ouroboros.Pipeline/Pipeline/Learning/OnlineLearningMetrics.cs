namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Performance metrics for online learning tracking.
/// </summary>
/// <param name="ProcessedCount">Total number of feedback items processed.</param>
/// <param name="AverageScore">Running average of feedback scores.</param>
/// <param name="ScoreVariance">Variance in feedback scores.</param>
/// <param name="UpdateCount">Number of parameter updates applied.</param>
/// <param name="AverageGradientMagnitude">Average magnitude of gradients.</param>
/// <param name="ConvergenceMetric">Metric indicating convergence (lower = more converged).</param>
/// <param name="LastUpdateTime">Timestamp of the last update.</param>
public sealed record OnlineLearningMetrics(
    int ProcessedCount,
    double AverageScore,
    double ScoreVariance,
    int UpdateCount,
    double AverageGradientMagnitude,
    double ConvergenceMetric,
    DateTime LastUpdateTime)
{
    /// <summary>
    /// Gets empty metrics representing no learning has occurred.
    /// </summary>
    public static OnlineLearningMetrics Empty => new(
        ProcessedCount: 0,
        AverageScore: 0.0,
        ScoreVariance: 0.0,
        UpdateCount: 0,
        AverageGradientMagnitude: 0.0,
        ConvergenceMetric: 1.0,
        LastUpdateTime: DateTime.MinValue);

    /// <summary>
    /// Updates metrics with a new feedback score using Welford's algorithm.
    /// </summary>
    /// <param name="score">The new feedback score.</param>
    /// <returns>Updated metrics.</returns>
    public OnlineLearningMetrics WithNewScore(double score)
    {
        var newCount = ProcessedCount + 1;
        var delta = score - AverageScore;
        var newAverage = AverageScore + (delta / newCount);
        var delta2 = score - newAverage;

        var newVariance = ProcessedCount == 0
            ? 0.0
            : ((ScoreVariance * ProcessedCount) + (delta * delta2)) / newCount;

        return this with
        {
            ProcessedCount = newCount,
            AverageScore = newAverage,
            ScoreVariance = newVariance,
            LastUpdateTime = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Updates metrics with gradient information from an update.
    /// </summary>
    /// <param name="gradientMagnitude">The magnitude of the gradient.</param>
    /// <returns>Updated metrics.</returns>
    public OnlineLearningMetrics WithGradient(double gradientMagnitude)
    {
        var newUpdateCount = UpdateCount + 1;
        var newAvgMagnitude = AverageGradientMagnitude + ((gradientMagnitude - AverageGradientMagnitude) / newUpdateCount);

        // Update convergence metric as exponential moving average of gradient magnitude
        var newConvergence = (0.95 * ConvergenceMetric) + (0.05 * gradientMagnitude);

        return this with
        {
            UpdateCount = newUpdateCount,
            AverageGradientMagnitude = newAvgMagnitude,
            ConvergenceMetric = newConvergence,
            LastUpdateTime = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Computes an overall performance score.
    /// </summary>
    /// <returns>A normalized performance score in [0, 1].</returns>
    public double ComputePerformanceScore()
    {
        if (ProcessedCount == 0)
        {
            return 0.0;
        }

        var scoreComponent = (AverageScore + 1.0) / 2.0; // Map [-1, 1] to [0, 1]
        var stabilityComponent = 1.0 / (1.0 + ScoreVariance);
        var convergenceComponent = 1.0 / (1.0 + ConvergenceMetric);

        return (0.5 * scoreComponent) + (0.25 * stabilityComponent) + (0.25 * convergenceComponent);
    }
}