namespace Ouroboros.Agent.MetaAI.MetaLearning;

/// <summary>
/// Configuration for meta-learner behavior.
/// </summary>
public sealed record MetaLearnerConfig(
    int MinEpisodesForOptimization = 10,
    int MaxFewShotExamples = 5,
    double MinConfidenceThreshold = 0.6,
    TimeSpan DefaultEvaluationWindow = default)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MetaLearnerConfig"/> class.
    /// </summary>
    public MetaLearnerConfig()
        : this(10, 5, 0.6, TimeSpan.FromDays(30))
    {
    }
}