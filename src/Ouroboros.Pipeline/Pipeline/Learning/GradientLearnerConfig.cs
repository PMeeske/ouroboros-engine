using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Configuration for gradient-based online learning.
/// </summary>
/// <param name="LearningRate">Base learning rate for updates (typically 0.001 to 0.1).</param>
/// <param name="Momentum">Momentum coefficient for smoothing updates (0 = no momentum, 0.9 = high momentum).</param>
/// <param name="AdaptiveLearningRate">Whether to use adaptive learning rates per parameter.</param>
/// <param name="GradientClipThreshold">Maximum gradient magnitude before clipping (prevents exploding gradients).</param>
/// <param name="MinConfidenceThreshold">Minimum confidence required to apply an update.</param>
/// <param name="BatchAccumulationSize">Number of updates to accumulate before applying.</param>
public sealed record GradientLearnerConfig(
    double LearningRate,
    double Momentum,
    bool AdaptiveLearningRate,
    double GradientClipThreshold,
    double MinConfidenceThreshold,
    int BatchAccumulationSize)
{
    /// <summary>
    /// Gets the default configuration with sensible hyperparameters.
    /// </summary>
    public static GradientLearnerConfig Default => new(
        LearningRate: 0.01,
        Momentum: 0.9,
        AdaptiveLearningRate: true,
        GradientClipThreshold: 1.0,
        MinConfidenceThreshold: 0.1,
        BatchAccumulationSize: 1);

    /// <summary>
    /// Creates a conservative configuration with slower, more stable learning.
    /// </summary>
    public static GradientLearnerConfig Conservative => new(
        LearningRate: 0.001,
        Momentum: 0.95,
        AdaptiveLearningRate: true,
        GradientClipThreshold: 0.5,
        MinConfidenceThreshold: 0.3,
        BatchAccumulationSize: 10);

    /// <summary>
    /// Creates an aggressive configuration for faster learning.
    /// </summary>
    public static GradientLearnerConfig Aggressive => new(
        LearningRate: 0.1,
        Momentum: 0.5,
        AdaptiveLearningRate: false,
        GradientClipThreshold: 5.0,
        MinConfidenceThreshold: 0.0,
        BatchAccumulationSize: 1);

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <returns>A Result indicating success or validation errors.</returns>
    public Result<Unit, string> Validate()
    {
        if (LearningRate <= 0 || LearningRate > 1)
        {
            return Result<Unit, string>.Failure($"LearningRate must be in (0, 1], got {LearningRate}.");
        }

        if (Momentum < 0 || Momentum >= 1)
        {
            return Result<Unit, string>.Failure($"Momentum must be in [0, 1), got {Momentum}.");
        }

        if (GradientClipThreshold <= 0)
        {
            return Result<Unit, string>.Failure($"GradientClipThreshold must be positive, got {GradientClipThreshold}.");
        }

        if (MinConfidenceThreshold < 0 || MinConfidenceThreshold > 1)
        {
            return Result<Unit, string>.Failure($"MinConfidenceThreshold must be in [0, 1], got {MinConfidenceThreshold}.");
        }

        if (BatchAccumulationSize <= 0)
        {
            return Result<Unit, string>.Failure($"BatchAccumulationSize must be positive, got {BatchAccumulationSize}.");
        }

        return Result<Unit, string>.Success(Unit.Value);
    }
}