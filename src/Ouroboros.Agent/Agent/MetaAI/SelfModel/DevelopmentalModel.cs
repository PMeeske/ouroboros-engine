// ==========================================================
// Developmental Model Implementation
// Piaget + Dreyfus Skill Model for tracking agent growth stages
// ==========================================================

namespace Ouroboros.Agent.MetaAI.SelfModel;

/// <summary>
/// Developmental stages based on the Dreyfus Skill Acquisition Model.
/// </summary>
public enum DevelopmentalStage
{
    Nascent,
    Developing,
    Competent,
    Proficient,
    Expert,
    Wise
}

/// <summary>
/// A developmental milestone achieved by the agent in a specific domain.
/// </summary>
/// <param name="Domain">The skill domain.</param>
/// <param name="Milestone">Name of the milestone.</param>
/// <param name="AchievedAt">When the milestone was reached.</param>
/// <param name="StageAtAchievement">The developmental stage at the time of achievement.</param>
/// <param name="PerformanceScore">The performance score that triggered the milestone.</param>
public sealed record DevelopmentalMilestone(
    string Domain,
    string Milestone,
    DateTime AchievedAt,
    DevelopmentalStage StageAtAchievement,
    double PerformanceScore);

/// <summary>
/// Tracks agent development across skill domains using a Piaget-inspired
/// stage model with Dreyfus skill acquisition levels and S-curve learning rates.
/// </summary>
public sealed class DevelopmentalModel
{
    private const double EmaSmoothingFactor = 0.3;

    private readonly Dictionary<string, double> _domainPerformance = new();
    private readonly Dictionary<string, List<double>> _performanceHistory = new();
    private readonly List<DevelopmentalMilestone> _milestones = new();
    private readonly object _lock = new();

    /// <summary>
    /// Returns the S-curve learning rate multiplier for a given stage.
    /// </summary>
    private static double GetLearningRate(DevelopmentalStage stage) => stage switch
    {
        DevelopmentalStage.Nascent => 0.3,
        DevelopmentalStage.Developing => 0.8,
        DevelopmentalStage.Competent => 1.0,
        DevelopmentalStage.Proficient => 0.6,
        DevelopmentalStage.Expert => 0.2,
        DevelopmentalStage.Wise => 0.1,
        _ => 0.5
    };

    /// <summary>
    /// Maps a cumulative performance score to a developmental stage.
    /// </summary>
    /// <param name="domain">The skill domain to query.</param>
    /// <returns>The current developmental stage for the domain.</returns>
    public DevelopmentalStage GetCurrentStage(string domain)
    {
        ArgumentNullException.ThrowIfNull(domain);

        lock (_lock)
        {
            if (!_domainPerformance.TryGetValue(domain, out double score))
                return DevelopmentalStage.Nascent;

            return score switch
            {
                < 0.2 => DevelopmentalStage.Nascent,
                < 0.4 => DevelopmentalStage.Developing,
                < 0.6 => DevelopmentalStage.Competent,
                < 0.8 => DevelopmentalStage.Proficient,
                < 0.95 => DevelopmentalStage.Expert,
                _ => DevelopmentalStage.Wise
            };
        }
    }

    /// <summary>
    /// Records a new performance observation for a domain, updating the
    /// exponential moving average weighted by the current S-curve learning rate.
    /// </summary>
    /// <param name="domain">The skill domain.</param>
    /// <param name="performanceScore">Observed performance score between 0.0 and 1.0.</param>
    /// <returns>The updated EMA performance score for the domain.</returns>
    public Result<double, string> RecordSkillProgress(string domain, double performanceScore)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return Result<double, string>.Failure("Domain must not be empty.");

        performanceScore = Math.Clamp(performanceScore, 0.0, 1.0);

        lock (_lock)
        {
            DevelopmentalStage currentStage = GetCurrentStage(domain);
            double learningRate = GetLearningRate(currentStage);

            // Weighted EMA: apply learning rate as a multiplier on the smoothing factor
            double effectiveAlpha = EmaSmoothingFactor * learningRate;

            if (!_domainPerformance.TryGetValue(domain, out double currentEma))
            {
                currentEma = performanceScore;
            }
            else
            {
                currentEma = effectiveAlpha * performanceScore + (1.0 - effectiveAlpha) * currentEma;
            }

            _domainPerformance[domain] = currentEma;

            // Track history
            if (!_performanceHistory.ContainsKey(domain))
                _performanceHistory[domain] = new List<double>();

            _performanceHistory[domain].Add(performanceScore);

            return Result<double, string>.Success(currentEma);
        }
    }

    /// <summary>
    /// Checks whether a named milestone has been crossed for a domain.
    /// If the current performance meets or exceeds the threshold and the
    /// milestone has not already been achieved, it is recorded.
    /// </summary>
    /// <param name="domain">The skill domain.</param>
    /// <param name="milestone">Name of the milestone to check.</param>
    /// <param name="threshold">Performance threshold for the milestone (0.0 to 1.0).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The milestone if newly achieved; failure if already achieved or not yet reached.</returns>
    public Task<Result<DevelopmentalMilestone, string>> CheckMilestoneAsync(
        string domain,
        string milestone,
        double threshold = 0.5,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(milestone);

        lock (_lock)
        {
            // Check if already achieved
            if (_milestones.Any(m =>
                    m.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase) &&
                    m.Milestone.Equals(milestone, StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult(
                    Result<DevelopmentalMilestone, string>.Failure(
                        $"Milestone '{milestone}' already achieved for domain '{domain}'."));
            }

            if (!_domainPerformance.TryGetValue(domain, out double currentScore) ||
                currentScore < threshold)
            {
                return Task.FromResult(
                    Result<DevelopmentalMilestone, string>.Failure(
                        $"Performance {currentScore:F2} has not reached threshold {threshold:F2}."));
            }

            DevelopmentalStage stage = GetCurrentStage(domain);
            var achieved = new DevelopmentalMilestone(
                domain,
                milestone,
                DateTime.UtcNow,
                stage,
                currentScore);

            _milestones.Add(achieved);

            return Task.FromResult(Result<DevelopmentalMilestone, string>.Success(achieved));
        }
    }

    /// <summary>
    /// Returns all milestones that have been achieved, ordered by time.
    /// </summary>
    /// <returns>List of achieved milestones.</returns>
    public IReadOnlyList<DevelopmentalMilestone> GetAchievedMilestones()
    {
        lock (_lock)
        {
            return _milestones
                .OrderBy(m => m.AchievedAt)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Returns all tracked domains and their current stages.
    /// </summary>
    /// <returns>Dictionary of domain to current developmental stage.</returns>
    public IReadOnlyDictionary<string, DevelopmentalStage> GetAllStages()
    {
        lock (_lock)
        {
            return _domainPerformance
                .ToDictionary(
                    kv => kv.Key,
                    kv => GetCurrentStage(kv.Key));
        }
    }
}
