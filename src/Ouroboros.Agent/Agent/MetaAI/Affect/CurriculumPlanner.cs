// ==========================================================
// Curriculum Planner — Self-directed learning curriculum
// Phase 115: Self-Directed Learning Curriculum
// Iaret identifies knowledge gaps from SelfAssessor dimension
// scores and MetaLearner exploration history, builds learning
// plans with difficulty progression, tracks mastery via accuracy
// on learned domains, and autonomously seeks learning opportunities.
// ALL logic is tensor/vector-based — no LLM calls.
// ==========================================================

using Ouroboros.Pipeline.Metacognition;

namespace Ouroboros.Agent.MetaAI.Affect;

/// <summary>
/// A single knowledge gap identified by comparing assessor scores against targets.
/// </summary>
/// <param name="Domain">Domain name (PerformanceDimension or capability name).</param>
/// <param name="CurrentScore">Current score or proficiency in [0, 1].</param>
/// <param name="TargetScore">Desired target score in [0, 1].</param>
/// <param name="Confidence">Confidence in the current assessment (0 = unknown, 1 = certain).</param>
/// <param name="GapMagnitude">TargetScore - CurrentScore, larger = more urgent.</param>
/// <param name="Trend">Whether the dimension is improving, declining, or stable.</param>
/// <param name="DetectedAt">When the gap was first identified.</param>
public sealed record KnowledgeGap(
    string Domain,
    double CurrentScore,
    double TargetScore,
    double Confidence,
    double GapMagnitude,
    Trend Trend,
    DateTime DetectedAt);

/// <summary>
/// Difficulty level for learning goals, derived from distance to known centroids.
/// </summary>
public enum DifficultyLevel
{
    /// <summary>Close to existing knowledge — reinforce and refine.</summary>
    Foundational = 0,

    /// <summary>Moderate distance — extend existing capabilities.</summary>
    Intermediate = 1,

    /// <summary>Far from known centroids — explore new territory.</summary>
    Advanced = 2,

    /// <summary>Very far from all known vectors — frontier learning.</summary>
    Frontier = 3,
}

/// <summary>
/// A learning goal within a curriculum plan.
/// </summary>
/// <param name="Id">Unique identifier for this goal.</param>
/// <param name="Domain">The knowledge domain this goal targets.</param>
/// <param name="Description">Human-readable description of the learning objective.</param>
/// <param name="Difficulty">Difficulty level based on vector distance from known centroids.</param>
/// <param name="Priority">Priority score (higher = more urgent). Derived from gap magnitude * urgency.</param>
/// <param name="MasteryThreshold">Score threshold at which this goal is considered mastered.</param>
/// <param name="CurrentMastery">Current mastery level (0.0 to 1.0).</param>
/// <param name="AttemptCount">Number of times this goal has been attempted.</param>
/// <param name="CreatedAt">When this goal was created.</param>
/// <param name="LastAttemptAt">When this goal was last attempted.</param>
public sealed record LearningGoal(
    Guid Id,
    string Domain,
    string Description,
    DifficultyLevel Difficulty,
    double Priority,
    double MasteryThreshold,
    double CurrentMastery,
    int AttemptCount,
    DateTime CreatedAt,
    DateTime? LastAttemptAt)
{
    /// <summary>Whether the goal has been mastered (current mastery meets or exceeds threshold).</summary>
    public bool IsMastered => CurrentMastery >= MasteryThreshold;

    /// <summary>Creates a new learning goal with updated mastery after an attempt.</summary>
    /// <param name="observedScore">Observed performance score (0.0 to 1.0).</param>
    /// <param name="emaAlpha">EMA smoothing factor (default 0.3 — recent performance weighted).</param>
    /// <returns>Updated goal with blended mastery and incremented attempt count.</returns>
    public LearningGoal WithMasteryUpdate(double observedScore, double emaAlpha = 0.3)
    {
        double clampedScore = Math.Clamp(observedScore, 0.0, 1.0);
        double newMastery = AttemptCount == 0
            ? clampedScore
            : (emaAlpha * clampedScore) + ((1.0 - emaAlpha) * CurrentMastery);

        return this with
        {
            CurrentMastery = Math.Clamp(newMastery, 0.0, 1.0),
            AttemptCount = AttemptCount + 1,
            LastAttemptAt = DateTime.UtcNow,
        };
    }
}

/// <summary>
/// A complete learning plan consisting of ordered goals with difficulty progression.
/// </summary>
/// <param name="Id">Unique plan identifier.</param>
/// <param name="Goals">Ordered learning goals (easiest first, progressing to harder).</param>
/// <param name="CreatedAt">When this plan was generated.</param>
/// <param name="CompletionRatio">Fraction of goals mastered (0.0 to 1.0).</param>
public sealed record LearningPlan(
    Guid Id,
    IReadOnlyList<LearningGoal> Goals,
    DateTime CreatedAt,
    double CompletionRatio);

/// <summary>
/// Tensor-centric curriculum planner that identifies knowledge gaps from
/// BayesianSelfAssessor dimension scores and AdaptiveMetaLearner exploration
/// history, generates learning plans with difficulty progression, and tracks
/// mastery via EMA on observed performance.
/// </summary>
/// <remarks>
/// <para>
/// Gap detection: compare SelfAssessor dimension scores against configurable
/// target thresholds. Dimensions scoring below target have a gap proportional
/// to (target - score) * (1 - confidence), prioritising uncertain weak areas.
/// </para>
/// <para>
/// Difficulty progression: goals are ordered by their gap magnitude (smallest
/// gaps first = foundational, largest = frontier), emulating curriculum
/// learning where you solidify nearby knowledge before exploring far.
/// </para>
/// <para>
/// Mastery tracking: EMA (alpha=0.3) on observed accuracy for each goal,
/// matching the ContinuouslyLearningAgent's EMA approach. A goal is "mastered"
/// when its EMA score exceeds the per-goal mastery threshold (default 0.7).
/// </para>
/// </remarks>
public sealed class CurriculumPlanner
{
    private readonly List<LearningGoal> _activeGoals = [];
    private readonly List<LearningGoal> _masteredGoals = [];
    private readonly Dictionary<string, KnowledgeGap> _gaps = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private LearningPlan? _currentPlan;
    private DateTime _lastPlanGeneration = DateTime.MinValue;

    /// <summary>
    /// Target scores per PerformanceDimension. Dimensions scoring below
    /// their target are identified as knowledge gaps.
    /// </summary>
    public Dictionary<string, double> DimensionTargets { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(PerformanceDimension.Accuracy)] = 0.7,
        [nameof(PerformanceDimension.Speed)] = 0.6,
        [nameof(PerformanceDimension.Creativity)] = 0.6,
        [nameof(PerformanceDimension.Consistency)] = 0.7,
        [nameof(PerformanceDimension.Adaptability)] = 0.65,
        [nameof(PerformanceDimension.Communication)] = 0.7,
    };

    /// <summary>
    /// Minimum interval between plan regeneration to prevent churn.
    /// </summary>
    public TimeSpan PlanRegenerationCooldown { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Maximum number of active learning goals in a plan.
    /// </summary>
    public int MaxActiveGoals { get; set; } = 8;

    /// <summary>
    /// Default mastery threshold for generated goals.
    /// </summary>
    public double DefaultMasteryThreshold { get; set; } = 0.7;

    /// <summary>
    /// Gets the current learning plan, or null if none has been generated.
    /// </summary>
    public LearningPlan? CurrentPlan
    {
        get
        {
            lock (_lock) { return _currentPlan; }
        }
    }

    /// <summary>
    /// Gets all currently identified knowledge gaps.
    /// </summary>
    public IReadOnlyDictionary<string, KnowledgeGap> Gaps
    {
        get
        {
            lock (_lock) { return new Dictionary<string, KnowledgeGap>(_gaps); }
        }
    }

    /// <summary>
    /// Gets the count of mastered goals (lifetime).
    /// </summary>
    public int MasteredGoalCount
    {
        get
        {
            lock (_lock) { return _masteredGoals.Count; }
        }
    }

    /// <summary>
    /// CUR-01: Identifies knowledge gaps by comparing SelfAssessor dimension
    /// scores against target thresholds. Also incorporates capability beliefs
    /// where uncertainty is high (unknown capabilities are gaps).
    /// </summary>
    /// <param name="selfAssessor">The BayesianSelfAssessor with dimension scores.</param>
    /// <returns>List of identified gaps sorted by magnitude descending.</returns>
    public IReadOnlyList<KnowledgeGap> DetectGaps(BayesianSelfAssessor selfAssessor)
    {
        ArgumentNullException.ThrowIfNull(selfAssessor);

        lock (_lock)
        {
            _gaps.Clear();

            // 1. Dimension-level gaps from SelfAssessor scores
            foreach (PerformanceDimension dimension in Enum.GetValues<PerformanceDimension>())
            {
                string name = dimension.ToString();
                double target = DimensionTargets.TryGetValue(name, out double t) ? t : 0.65;

                var result = selfAssessor.AssessDimensionAsync(dimension).GetAwaiter().GetResult();
                if (!result.IsSuccess) continue;

                DimensionScore score = result.Value;

                // Gap = target - current score, amplified by uncertainty (1 - confidence)
                double gapMagnitude = target - score.Score;
                if (gapMagnitude > 0.01) // Only track actual gaps
                {
                    // Uncertainty amplifier: less confident assessments have bigger effective gaps
                    double uncertaintyBoost = 1.0 + ((1.0 - score.Confidence) * 0.5);
                    double effectiveGap = gapMagnitude * uncertaintyBoost;

                    _gaps[name] = new KnowledgeGap(
                        Domain: name,
                        CurrentScore: score.Score,
                        TargetScore: target,
                        Confidence: score.Confidence,
                        GapMagnitude: effectiveGap,
                        Trend: score.Trend,
                        DetectedAt: DateTime.UtcNow);
                }
            }

            // 2. Capability-level gaps from belief proficiency
            var beliefs = selfAssessor.GetAllBeliefs();
            foreach (var (capName, belief) in beliefs)
            {
                // High uncertainty OR low proficiency = knowledge gap
                if (belief.Uncertainty > 0.5 || belief.Proficiency < 0.5)
                {
                    double gapMagnitude = (1.0 - belief.Proficiency) * (0.5 + (belief.Uncertainty * 0.5));
                    if (gapMagnitude > 0.1 && !_gaps.ContainsKey(capName))
                    {
                        _gaps[capName] = new KnowledgeGap(
                            Domain: capName,
                            CurrentScore: belief.Proficiency,
                            TargetScore: 0.7,
                            Confidence: 1.0 - belief.Uncertainty,
                            GapMagnitude: gapMagnitude,
                            Trend: Trend.Unknown,
                            DetectedAt: DateTime.UtcNow);
                    }
                }
            }

            return _gaps.Values
                .OrderByDescending(g => g.GapMagnitude)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// CUR-02: Generates a learning plan from detected gaps with difficulty
    /// progression. Goals are ordered from foundational (smallest gaps, closest
    /// to existing knowledge) to frontier (largest gaps, unknown territory).
    /// </summary>
    /// <param name="gaps">Knowledge gaps to address (from <see cref="DetectGaps"/>).</param>
    /// <returns>A new learning plan, or the existing plan if cooldown has not elapsed.</returns>
    public LearningPlan GeneratePlan(IReadOnlyList<KnowledgeGap> gaps)
    {
        ArgumentNullException.ThrowIfNull(gaps);

        lock (_lock)
        {
            // Check cooldown
            if (_currentPlan != null && DateTime.UtcNow - _lastPlanGeneration < PlanRegenerationCooldown)
            {
                return _currentPlan;
            }

            // Generate goals from gaps, ordered by difficulty (ascending)
            var goals = new List<LearningGoal>();

            foreach (KnowledgeGap gap in gaps.OrderBy(g => g.GapMagnitude))
            {
                if (goals.Count >= MaxActiveGoals) break;

                // Skip gaps where we're already trending positive with high confidence
                if (gap.Trend == Trend.Improving && gap.Confidence > 0.7 && gap.GapMagnitude < 0.15)
                    continue;

                // Determine difficulty from gap magnitude
                DifficultyLevel difficulty = gap.GapMagnitude switch
                {
                    < 0.15 => DifficultyLevel.Foundational,
                    < 0.30 => DifficultyLevel.Intermediate,
                    < 0.50 => DifficultyLevel.Advanced,
                    _ => DifficultyLevel.Frontier,
                };

                // Check if this gap already has an active goal
                LearningGoal? existing = _activeGoals.Find(g =>
                    string.Equals(g.Domain, gap.Domain, StringComparison.OrdinalIgnoreCase));

                if (existing != null && !existing.IsMastered)
                {
                    // Preserve existing goal with its mastery progress
                    goals.Add(existing);
                }
                else if (existing == null || existing.IsMastered)
                {
                    // Create new goal
                    string description = GenerateGoalDescription(gap, difficulty);

                    // Priority: gap magnitude weighted by declining trends
                    double trendWeight = gap.Trend switch
                    {
                        Trend.Declining => 1.5,
                        Trend.Stable => 1.0,
                        Trend.Improving => 0.7,
                        _ => 1.2, // Unknown gets slight boost
                    };
                    double priority = gap.GapMagnitude * trendWeight;

                    goals.Add(new LearningGoal(
                        Id: Guid.NewGuid(),
                        Domain: gap.Domain,
                        Description: description,
                        Difficulty: difficulty,
                        Priority: priority,
                        MasteryThreshold: DefaultMasteryThreshold,
                        CurrentMastery: 0.0,
                        AttemptCount: 0,
                        CreatedAt: DateTime.UtcNow,
                        LastAttemptAt: null));
                }
            }

            // Move previously mastered goals
            foreach (LearningGoal prev in _activeGoals)
            {
                if (prev.IsMastered && !_masteredGoals.Any(m => m.Id == prev.Id))
                {
                    _masteredGoals.Add(prev);
                }
            }

            _activeGoals.Clear();
            _activeGoals.AddRange(goals);

            double completionRatio = goals.Count > 0
                ? (double)goals.Count(g => g.IsMastered) / goals.Count
                : 0.0;

            _currentPlan = new LearningPlan(
                Id: Guid.NewGuid(),
                Goals: goals.AsReadOnly(),
                CreatedAt: DateTime.UtcNow,
                CompletionRatio: completionRatio);

            _lastPlanGeneration = DateTime.UtcNow;
            return _currentPlan;
        }
    }

    /// <summary>
    /// CUR-02/CUR-03: Records a learning attempt for a domain and updates
    /// mastery via EMA. Called when an autonomous learning goal completes.
    /// </summary>
    /// <param name="domain">The domain that was practiced.</param>
    /// <param name="observedScore">Observed performance (0.0 to 1.0).</param>
    /// <returns>True if mastery was updated; false if no active goal exists for the domain.</returns>
    public bool RecordLearningAttempt(string domain, double observedScore)
    {
        ArgumentNullException.ThrowIfNull(domain);

        lock (_lock)
        {
            int index = _activeGoals.FindIndex(g =>
                string.Equals(g.Domain, domain, StringComparison.OrdinalIgnoreCase));

            if (index < 0) return false;

            LearningGoal updated = _activeGoals[index].WithMasteryUpdate(observedScore);
            _activeGoals[index] = updated;

            // Move to mastered if threshold reached
            if (updated.IsMastered && !_masteredGoals.Any(m => m.Id == updated.Id))
            {
                _masteredGoals.Add(updated);
            }

            // Regenerate plan with updated goals
            if (_currentPlan != null)
            {
                double completionRatio = _activeGoals.Count > 0
                    ? (double)_activeGoals.Count(g => g.IsMastered) / _activeGoals.Count
                    : 0.0;

                _currentPlan = _currentPlan with
                {
                    Goals = _activeGoals.AsReadOnly(),
                    CompletionRatio = completionRatio,
                };
            }

            return true;
        }
    }

    /// <summary>
    /// CUR-03: Gets the next un-mastered learning goal to pursue autonomously.
    /// Returns the highest-priority goal that hasn't been attempted recently
    /// (at least 2 minutes since last attempt to avoid repetition).
    /// </summary>
    /// <returns>The next goal to pursue, or null if all goals are mastered or on cooldown.</returns>
    public LearningGoal? GetNextLearningOpportunity()
    {
        lock (_lock)
        {
            DateTime cooldownThreshold = DateTime.UtcNow.AddMinutes(-2);

            return _activeGoals
                .Where(g => !g.IsMastered)
                .Where(g => g.LastAttemptAt == null || g.LastAttemptAt < cooldownThreshold)
                .OrderByDescending(g => g.Priority)
                .ThenBy(g => g.AttemptCount) // Prefer less-attempted goals
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Gets gap descriptions suitable for feeding into MotivationEngine's
    /// TickAndGenerateGoals(knowledgeGaps) parameter.
    /// </summary>
    /// <returns>List of gap description strings for goal template {gap} substitution.</returns>
    public IReadOnlyList<string> GetGapDescriptionsForMotivation()
    {
        lock (_lock)
        {
            return _gaps.Values
                .OrderByDescending(g => g.GapMagnitude)
                .Take(5)
                .Select(g => $"{g.Domain} (score: {g.CurrentScore:F2}, target: {g.TargetScore:F2})")
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Gets a diagnostic summary of the curriculum state.
    /// </summary>
    public string GetSummary()
    {
        lock (_lock)
        {
            int active = _activeGoals.Count;
            int mastered = _masteredGoals.Count;
            int gaps = _gaps.Count;
            double avgMastery = _activeGoals.Count > 0
                ? _activeGoals.Average(g => g.CurrentMastery) : 0.0;

            var topGap = _gaps.Values
                .OrderByDescending(g => g.GapMagnitude)
                .FirstOrDefault();

            string topGapStr = topGap != null
                ? $"{topGap.Domain} (gap={topGap.GapMagnitude:F2})"
                : "none";

            return $"[Curriculum] gaps={gaps}, active={active}, mastered={mastered}, " +
                   $"avgMastery={avgMastery:F2}, topGap={topGapStr}";
        }
    }

    /// <summary>
    /// Generates a goal description from a knowledge gap without LLM.
    /// Uses template patterns matching MotivationEngine's goal template style.
    /// </summary>
    private static string GenerateGoalDescription(KnowledgeGap gap, DifficultyLevel difficulty)
    {
        string action = difficulty switch
        {
            DifficultyLevel.Foundational => "Reinforce and solidify understanding of",
            DifficultyLevel.Intermediate => "Extend capabilities in",
            DifficultyLevel.Advanced => "Develop advanced competency in",
            DifficultyLevel.Frontier => "Explore and pioneer learning in",
            _ => "Improve performance in",
        };

        string trendNote = gap.Trend switch
        {
            Trend.Declining => " (declining — needs attention)",
            Trend.Stable => " (stagnant — needs new approach)",
            Trend.Unknown => " (unexplored — needs assessment)",
            _ => "",
        };

        return $"{action} {gap.Domain}{trendNote}";
    }
}
