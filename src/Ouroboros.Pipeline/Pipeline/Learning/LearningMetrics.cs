namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Tracks learning performance metrics over time.
/// Provides statistical measures of learning progress and efficiency.
/// </summary>
/// <param name="TotalEpisodes">Total number of learning episodes completed.</param>
/// <param name="AverageReward">Mean reward across all episodes.</param>
/// <param name="RewardVariance">Variance in episode rewards.</param>
/// <param name="ConvergenceRate">Rate at which rewards are stabilizing (lower = more stable).</param>
/// <param name="LearningEfficiency">Reward improvement per episode (higher = faster learning).</param>
/// <param name="Timestamps">Ordered list of measurement timestamps.</param>
public sealed record LearningMetrics(
    int TotalEpisodes,
    double AverageReward,
    double RewardVariance,
    double ConvergenceRate,
    double LearningEfficiency,
    ImmutableList<DateTime> Timestamps)
{
    /// <summary>
    /// Gets empty metrics representing no learning has occurred.
    /// </summary>
    public static LearningMetrics Empty => new(
        TotalEpisodes: 0,
        AverageReward: 0.0,
        RewardVariance: 0.0,
        ConvergenceRate: 1.0,
        LearningEfficiency: 0.0,
        Timestamps: ImmutableList<DateTime>.Empty);

    /// <summary>
    /// Creates metrics from a sequence of episode rewards.
    /// </summary>
    /// <param name="rewards">The sequence of rewards from each episode.</param>
    /// <returns>Computed learning metrics.</returns>
    public static LearningMetrics FromRewards(IEnumerable<double> rewards)
    {
        var rewardList = rewards.ToList();
        if (rewardList.Count == 0)
        {
            return Empty;
        }

        var totalEpisodes = rewardList.Count;
        var averageReward = rewardList.Average();
        var variance = rewardList.Sum(r => Math.Pow(r - averageReward, 2)) / totalEpisodes;

        // Compute convergence rate as change in rolling average
        var convergenceRate = ComputeConvergenceRate(rewardList);

        // Compute learning efficiency as reward improvement per episode
        var efficiency = ComputeLearningEfficiency(rewardList);

        return new LearningMetrics(
            TotalEpisodes: totalEpisodes,
            AverageReward: averageReward,
            RewardVariance: variance,
            ConvergenceRate: convergenceRate,
            LearningEfficiency: efficiency,
            Timestamps: ImmutableList.Create(DateTime.UtcNow));
    }

    /// <summary>
    /// Updates metrics with a new episode reward.
    /// Uses Welford's online algorithm for variance computation.
    /// </summary>
    /// <param name="newReward">The reward from the latest episode.</param>
    /// <returns>Updated learning metrics.</returns>
    public LearningMetrics WithNewReward(double newReward)
    {
        var newTotal = TotalEpisodes + 1;
        var delta = newReward - AverageReward;
        var newAverage = AverageReward + (delta / newTotal);
        var delta2 = newReward - newAverage;

        // Welford's online variance update
        var newVariance = TotalEpisodes == 0
            ? 0.0
            : ((RewardVariance * TotalEpisodes) + (delta * delta2)) / newTotal;

        // Update convergence rate (exponential moving average of absolute delta)
        var newConvergence = (0.9 * ConvergenceRate) + (0.1 * Math.Abs(delta));

        // Update efficiency (positive improvement trend)
        var improvementRate = delta > 0 ? delta / Math.Max(Math.Abs(AverageReward), 1e-6) : 0.0;
        var newEfficiency = (0.95 * LearningEfficiency) + (0.05 * improvementRate);

        return this with
        {
            TotalEpisodes = newTotal,
            AverageReward = newAverage,
            RewardVariance = newVariance,
            ConvergenceRate = newConvergence,
            LearningEfficiency = newEfficiency,
            Timestamps = Timestamps.Add(DateTime.UtcNow),
        };
    }

    /// <summary>
    /// Computes a normalized performance score for strategy comparison.
    /// Higher scores indicate better learning performance.
    /// </summary>
    /// <returns>A normalized performance score in [0, 1].</returns>
    public double ComputePerformanceScore()
    {
        if (TotalEpisodes == 0)
        {
            return 0.0;
        }

        // Normalize components
        var rewardScore = Math.Tanh(AverageReward); // Maps to [-1, 1]
        var stabilityScore = 1.0 / (1.0 + RewardVariance); // Higher variance = lower score
        var convergenceScore = 1.0 / (1.0 + ConvergenceRate); // Lower convergence rate = better
        var efficiencyScore = Math.Tanh(LearningEfficiency * 10); // Scale efficiency

        // Weighted combination
        return (0.4 * rewardScore) +
               (0.2 * stabilityScore) +
               (0.2 * convergenceScore) +
               (0.2 * efficiencyScore);
    }

    private static double ComputeConvergenceRate(List<double> rewards)
    {
        if (rewards.Count < 10)
        {
            return 1.0;
        }

        // Compare recent vs overall average
        var recentCount = Math.Min(10, rewards.Count / 4);
        var recentAvg = rewards.Skip(rewards.Count - recentCount).Average();
        var overallAvg = rewards.Average();

        return Math.Abs(recentAvg - overallAvg) / Math.Max(Math.Abs(overallAvg), 1e-6);
    }

    private static double ComputeLearningEfficiency(List<double> rewards)
    {
        if (rewards.Count < 2)
        {
            return 0.0;
        }

        // Linear regression slope normalized by count
        var n = rewards.Count;
        var sumX = (n * (n - 1)) / 2.0;
        var sumX2 = (n * (n - 1) * ((2 * n) - 1)) / 6.0;
        var sumY = rewards.Sum();
        var sumXY = rewards.Select((r, i) => r * i).Sum();

        var slope = ((n * sumXY) - (sumX * sumY)) / ((n * sumX2) - (sumX * sumX));

        return slope;
    }
}