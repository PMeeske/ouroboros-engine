using System.Runtime.CompilerServices;

namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Adaptive meta-learner implementing Bayesian optimization-inspired strategy adaptation.
/// Tracks historical performance to guide hyperparameter exploration and exploitation.
/// </summary>
public sealed class AdaptiveMetaLearner : IMetaLearner
{
    private readonly ConcurrentDictionary<Guid, List<StrategyPerformance>> _history = new();
    private readonly Random _random;
    private readonly double _explorationWeight;
    private readonly int _historyLimit;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveMetaLearner"/> class.
    /// </summary>
    /// <param name="explorationWeight">Weight for exploration vs exploitation (default: 0.2).</param>
    /// <param name="historyLimit">Maximum history entries per strategy (default: 100).</param>
    /// <param name="seed">Optional random seed for reproducibility.</param>
    public AdaptiveMetaLearner(
        double explorationWeight = 0.2,
        int historyLimit = 100,
        int? seed = null)
    {
        _explorationWeight = Math.Clamp(explorationWeight, 0.0, 1.0);
        _historyLimit = Math.Max(1, historyLimit);
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double EvaluateStrategy(LearningStrategy strategy, LearningMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(metrics);

        // Base score from metrics
        var baseScore = metrics.ComputePerformanceScore();

        // Bonus for exploration (UCB-style)
        var explorationBonus = ComputeExplorationBonus(strategy.Id);

        // Penalty for invalid configurations
        var validationPenalty = strategy.Validate().IsFailure ? -0.5 : 0.0;

        // Hyperparameter quality heuristics
        var hyperparamScore = ComputeHyperparameterScore(strategy, metrics);

        return baseScore + (explorationBonus * _explorationWeight) + validationPenalty + (hyperparamScore * 0.1);
    }

    /// <inheritdoc/>
    public Result<LearningStrategy, string> AdaptStrategy(LearningStrategy current, LearningMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(metrics);

        try
        {
            // Record current performance
            RecordPerformance(current, metrics);

            // Analyze learning state
            var state = AnalyzeLearningState(metrics);

            // Apply Bayesian-inspired adaptation
            var adapted = state switch
            {
                LearningState.Exploring => AdaptForExploration(current, metrics),
                LearningState.Converging => AdaptForConvergence(current, metrics),
                LearningState.Converged => AdaptForExploitation(current, metrics),
                LearningState.Diverging => AdaptForRecovery(current, metrics),
                LearningState.Stagnant => AdaptForEscapeStagnation(current, metrics),
                _ => current,
            };

            // Add Gaussian noise for exploration
            var explored = ApplyGaussianExploration(adapted);

            return Result<LearningStrategy, string>.Success(explored);
        }
        catch (Exception ex)
        {
            return Result<LearningStrategy, string>.Failure($"Strategy adaptation failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Result<LearningStrategy, string> SelectBestStrategy(
        IEnumerable<LearningStrategy> strategies,
        LearningMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        ArgumentNullException.ThrowIfNull(metrics);

        var strategyList = strategies.ToList();
        if (strategyList.Count == 0)
        {
            return Result<LearningStrategy, string>.Failure("No strategies provided for selection.");
        }

        try
        {
            // Evaluate all strategies
            var scored = strategyList
                .Select(s => (Strategy: s, Score: EvaluateStrategy(s, metrics)))
                .OrderByDescending(x => x.Score)
                .ToList();

            // Apply softmax selection for stochastic exploration
            var selected = SoftmaxSelect(scored);

            return Result<LearningStrategy, string>.Success(selected);
        }
        catch (Exception ex)
        {
            return Result<LearningStrategy, string>.Failure($"Strategy selection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the historical performance data for a strategy.
    /// </summary>
    /// <param name="strategyId">The strategy identifier.</param>
    /// <returns>The performance history, if available.</returns>
    public Option<IReadOnlyList<StrategyPerformance>> GetHistory(Guid strategyId)
    {
        return _history.TryGetValue(strategyId, out var history)
            ? Option<IReadOnlyList<StrategyPerformance>>.Some(history.AsReadOnly())
            : Option<IReadOnlyList<StrategyPerformance>>.None();
    }

    private void RecordPerformance(LearningStrategy strategy, LearningMetrics metrics)
    {
        var performance = new StrategyPerformance(
            StrategyId: strategy.Id,
            Score: metrics.ComputePerformanceScore(),
            Timestamp: DateTime.UtcNow,
            Hyperparameters: ImmutableDictionary<string, double>.Empty
                .Add("learning_rate", strategy.LearningRate)
                .Add("exploration_rate", strategy.ExplorationRate)
                .Add("discount_factor", strategy.DiscountFactor)
                .Add("batch_size", strategy.BatchSize));

        var history = _history.GetOrAdd(strategy.Id, _ => new List<StrategyPerformance>());

        lock (history)
        {
            history.Add(performance);

            // Trim to limit
            while (history.Count > _historyLimit)
            {
                history.RemoveAt(0);
            }
        }
    }

    private double ComputeExplorationBonus(Guid strategyId)
    {
        if (!_history.TryGetValue(strategyId, out var history) || history.Count == 0)
        {
            return 1.0; // Maximum bonus for unexplored strategies
        }

        // UCB-style bonus: sqrt(ln(total) / visits)
        var totalVisits = _history.Values.Sum(h => h.Count);
        var strategyVisits = history.Count;

        return Math.Sqrt(Math.Log(totalVisits + 1) / (strategyVisits + 1));
    }

    private static double ComputeHyperparameterScore(LearningStrategy strategy, LearningMetrics metrics)
    {
        var score = 0.0;

        // Prefer lower learning rates when converging
        if (metrics.ConvergenceRate < 0.1)
        {
            score += strategy.LearningRate < 0.001 ? 0.1 : -0.05;
        }

        // Prefer higher exploration when performance is low
        if (metrics.AverageReward < 0)
        {
            score += strategy.ExplorationRate > 0.3 ? 0.1 : -0.05;
        }

        // Prefer larger batch sizes when variance is high
        if (metrics.RewardVariance > 0.5)
        {
            score += strategy.BatchSize >= 64 ? 0.1 : -0.05;
        }

        return score;
    }

    private static LearningState AnalyzeLearningState(LearningMetrics metrics)
    {
        if (metrics.TotalEpisodes < 10)
        {
            return LearningState.Exploring;
        }

        // Check for convergence
        if (metrics.ConvergenceRate < 0.05 && metrics.RewardVariance < 0.1)
        {
            return LearningState.Converged;
        }

        // Check for divergence (increasing variance)
        if (metrics.RewardVariance > 1.0 && metrics.LearningEfficiency < 0)
        {
            return LearningState.Diverging;
        }

        // Check for stagnation
        if (Math.Abs(metrics.LearningEfficiency) < 0.001 && metrics.TotalEpisodes > 50)
        {
            return LearningState.Stagnant;
        }

        // Check for converging
        if (metrics.ConvergenceRate < 0.2 && metrics.LearningEfficiency > 0)
        {
            return LearningState.Converging;
        }

        return LearningState.Exploring;
    }

    private static LearningStrategy AdaptForExploration(LearningStrategy current, LearningMetrics metrics)
    {
        // Increase exploration and learning rate
        return current
            .WithLearningRate(Math.Min(current.LearningRate * 1.5, 0.1))
            .WithExplorationRate(Math.Min(current.ExplorationRate * 1.2, 0.5));
    }

    private static LearningStrategy AdaptForConvergence(LearningStrategy current, LearningMetrics metrics)
    {
        // Gradually decrease rates
        return current
            .WithLearningRate(current.LearningRate * 0.9)
            .WithExplorationRate(current.ExplorationRate * 0.95);
    }

    private static LearningStrategy AdaptForExploitation(LearningStrategy current, LearningMetrics metrics)
    {
        // Minimize exploration, fine-tune learning rate
        return current
            .WithLearningRate(current.LearningRate * 0.5)
            .WithExplorationRate(Math.Max(current.ExplorationRate * 0.5, 0.01));
    }

    private static LearningStrategy AdaptForRecovery(LearningStrategy current, LearningMetrics metrics)
    {
        // Reduce learning rate significantly, increase exploration
        return current
            .WithLearningRate(current.LearningRate * 0.3)
            .WithExplorationRate(Math.Min(current.ExplorationRate + 0.2, 0.8));
    }

    private static LearningStrategy AdaptForEscapeStagnation(LearningStrategy current, LearningMetrics metrics)
    {
        // Bump learning rate and exploration to escape local optima
        return current
            .WithLearningRate(Math.Min(current.LearningRate * 3.0, 0.05))
            .WithExplorationRate(Math.Min(current.ExplorationRate + 0.3, 0.7));
    }

    private LearningStrategy ApplyGaussianExploration(LearningStrategy strategy)
    {
        // Apply small Gaussian perturbations
        var lrNoise = 1.0 + (SampleGaussian() * 0.1);
        var erNoise = 1.0 + (SampleGaussian() * 0.05);
        var dfNoise = 1.0 + (SampleGaussian() * 0.02);

        return strategy
            .WithLearningRate(strategy.LearningRate * lrNoise)
            .WithExplorationRate(strategy.ExplorationRate * erNoise)
            .WithDiscountFactor(strategy.DiscountFactor * dfNoise);
    }

    private double SampleGaussian()
    {
        // Box-Muller transform
        var u1 = 1.0 - _random.NextDouble();
        var u2 = 1.0 - _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    private LearningStrategy SoftmaxSelect(List<(LearningStrategy Strategy, double Score)> scored)
    {
        if (scored.Count == 1)
        {
            return scored[0].Strategy;
        }

        // Softmax with temperature
        var temperature = 0.5;
        var maxScore = scored.Max(s => s.Score);
        var expScores = scored.Select(s => Math.Exp((s.Score - maxScore) / temperature)).ToList();
        var sumExp = expScores.Sum();

        var probabilities = expScores.Select(e => e / sumExp).ToList();

        // Sample from distribution
        var sample = _random.NextDouble();
        var cumulative = 0.0;

        for (int i = 0; i < probabilities.Count; i++)
        {
            cumulative += probabilities[i];
            if (sample <= cumulative)
            {
                return scored[i].Strategy;
            }
        }

        return scored[^1].Strategy;
    }
}