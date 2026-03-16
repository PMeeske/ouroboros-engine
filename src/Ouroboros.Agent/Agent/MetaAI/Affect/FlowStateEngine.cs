// ==========================================================
// Flow State Engine
// Phase 3: Affective Dynamics - Csikszentmihalyi flow model
// Models 8 flow channels based on skill/challenge balance
// ==========================================================

namespace Ouroboros.Agent.MetaAI.Affect;



/// <summary>
/// Record of a single task engagement episode.
/// </summary>
/// <param name="TaskId">Identifier of the engaged task.</param>
/// <param name="ChallengeLevel">Challenge level during the task (0.0 to 1.0).</param>
/// <param name="PerformanceQuality">Quality of performance achieved (0.0 to 1.0).</param>
/// <param name="Channel">The flow channel during engagement.</param>
/// <param name="Duration">Duration of the engagement.</param>
/// <param name="Timestamp">When the engagement was recorded.</param>
public sealed record TaskEngagement(
    string TaskId,
    double ChallengeLevel,
    double PerformanceQuality,
    FlowState Channel,
    TimeSpan Duration,
    DateTime Timestamp);

/// <summary>
/// Implements the Csikszentmihalyi flow model for optimal experience tracking.
/// Flow occurs when the challenge/skill ratio is between 0.8 and 1.2 and both exceed 0.5.
/// Tracks flow episodes, duration, and entry rate over time.
/// </summary>
public sealed class FlowStateEngine
{
    private readonly List<FlowAssessment> _assessmentHistory = [];
    private readonly List<TaskEngagement> _engagementHistory = [];
    private readonly object _lock = new();

    /// <summary>Time distortion factors by flow channel. Flow compresses perceived time; boredom expands it.</summary>
    private static readonly Dictionary<FlowState, double> TimeDistortionFactors = new()
    {
        [FlowState.Apathy] = 1.8,
        [FlowState.Worry] = 1.3,
        [FlowState.Anxiety] = 1.5,
        [FlowState.Arousal] = 0.8,
        [FlowState.Flow] = 0.5,
        [FlowState.Control] = 0.7,
        [FlowState.Relaxation] = 1.0,
        [FlowState.Boredom] = 2.0,
    };

    /// <summary>
    /// Assesses the current flow state based on skill level, challenge level, and absorption.
    /// </summary>
    /// <param name="skillLevel">Current skill level for the task (0.0 to 1.0).</param>
    /// <param name="challengeLevel">Current challenge level of the task (0.0 to 1.0).</param>
    /// <param name="absorption">Degree of cognitive absorption (0.0 to 1.0).</param>
    /// <returns>A flow assessment describing the current channel and state.</returns>
    public Result<FlowAssessment, string> AssessFlowState(
        double skillLevel,
        double challengeLevel,
        double absorption)
    {
        skillLevel = Math.Clamp(skillLevel, 0.0, 1.0);
        challengeLevel = Math.Clamp(challengeLevel, 0.0, 1.0);
        absorption = Math.Clamp(absorption, 0.0, 1.0);

        var channel = DetermineChannel(skillLevel, challengeLevel);
        var ratio = skillLevel > 0.01 ? challengeLevel / skillLevel : 0.0;
        var isInFlow = channel == FlowState.Flow
                       && ratio >= 0.8 && ratio <= 1.2
                       && skillLevel > 0.5 && challengeLevel > 0.5;

        var timeDistortion = TimeDistortionFactors.GetValueOrDefault(channel, 1.0);

        // Absorption further modifies time distortion in flow
        if (isInFlow)
        {
            timeDistortion *= (1.0 - (absorption * 0.3));
        }

        var assessment = new FlowAssessment(
            State: channel,
            SkillLevel: skillLevel,
            ChallengeLevel: challengeLevel,
            Absorption: absorption,
            TimeDistortion: Math.Round(timeDistortion, 3),
            IntrinsicReward: isInFlow ? Math.Round(absorption * 0.8 + 0.2, 3) : Math.Round(absorption * 0.3, 3));

        lock (_lock)
        {
            _assessmentHistory.Add(assessment);
        }

        return Result<FlowAssessment, string>.Success(assessment);
    }

    /// <summary>
    /// Records a task engagement episode for flow tracking and statistics.
    /// </summary>
    /// <param name="taskId">The identifier of the task.</param>
    /// <param name="challengeLevel">Challenge level during the task (0.0 to 1.0).</param>
    /// <param name="performanceQuality">Quality of performance achieved (0.0 to 1.0).</param>
    /// <param name="duration">Duration of the engagement.</param>
    public void RecordTaskEngagement(
        string taskId,
        double challengeLevel,
        double performanceQuality,
        TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(taskId);

        challengeLevel = Math.Clamp(challengeLevel, 0.0, 1.0);
        performanceQuality = Math.Clamp(performanceQuality, 0.0, 1.0);

        // Infer skill from performance quality
        var channel = DetermineChannel(performanceQuality, challengeLevel);

        var engagement = new TaskEngagement(
            TaskId: taskId,
            ChallengeLevel: challengeLevel,
            PerformanceQuality: performanceQuality,
            Channel: channel,
            Duration: duration,
            Timestamp: DateTime.UtcNow);

        lock (_lock)
        {
            _engagementHistory.Add(engagement);
        }
    }

    /// <summary>
    /// Generates optimization recommendations to move the agent toward a flow state.
    /// </summary>
    /// <param name="taskDescription">Description of the task to optimize.</param>
    /// <param name="currentSkillLevel">The agent's current skill level for this task (0.0 to 1.0).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Optimization recommendations for achieving flow.</returns>
    public Task<Result<FlowOptimization, string>> OptimizeForFlowAsync(
        string taskDescription,
        double currentSkillLevel,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(taskDescription);

        if (ct.IsCancellationRequested)
            return Task.FromResult(Result<FlowOptimization, string>.Failure("Operation was cancelled."));

        currentSkillLevel = Math.Clamp(currentSkillLevel, 0.0, 1.0);

        // Ideal challenge for flow: match skill level (ratio ~1.0), both above 0.5
        var idealChallenge = currentSkillLevel;
        var currentChannel = DetermineChannel(currentSkillLevel, idealChallenge);

        double challengeAdjustment;
        double skillUtilization;
        double flowProbability;
        string rationale;

        if (currentSkillLevel < 0.5)
        {
            // Skill too low for flow: recommend easier challenges to build skill
            challengeAdjustment = -0.2;
            skillUtilization = 0.9;
            flowProbability = 0.2;
            rationale = "Skill level is below flow threshold (0.5). Reduce challenge to build competence before targeting flow.";
        }
        else
        {
            // Skill adequate: match challenge to skill
            challengeAdjustment = currentSkillLevel - 0.5; // Push challenge toward skill level
            skillUtilization = 0.85;
            flowProbability = Math.Min(0.4 + (currentSkillLevel * 0.5), 0.9);
            rationale = $"Set challenge near skill level ({currentSkillLevel:F2}) to enter flow channel. " +
                        $"Both dimensions exceed 0.5 threshold.";
        }

        var optimization = new FlowOptimization(
            RecommendedChallenge: Math.Round(Math.Clamp(currentSkillLevel + challengeAdjustment, 0.0, 1.0), 3),
            SuggestedAdjustment: rationale,
            PredictedFlowProbability: Math.Round(flowProbability, 3));

        return Task.FromResult(Result<FlowOptimization, string>.Success(optimization));
    }

    /// <summary>
    /// Gets the flow entry rate: fraction of assessments where flow was achieved.
    /// </summary>
    /// <returns>Flow entry rate (0.0 to 1.0), or 0.0 if no assessments recorded.</returns>
    public double GetFlowEntryRate()
    {
        lock (_lock)
        {
            if (_assessmentHistory.Count == 0)
                return 0.0;

            return (double)_assessmentHistory.Count(a => a.State == FlowState.Flow) / _assessmentHistory.Count;
        }
    }

    /// <summary>
    /// Gets the average duration of flow episodes from engagement history.
    /// </summary>
    /// <returns>Average flow episode duration, or <see cref="TimeSpan.Zero"/> if none recorded.</returns>
    public TimeSpan GetAverageFlowDuration()
    {
        lock (_lock)
        {
            var flowEpisodes = _engagementHistory
                .Where(e => e.Channel == FlowState.Flow)
                .ToList();

            if (flowEpisodes.Count == 0)
                return TimeSpan.Zero;

            var avgTicks = (long)flowEpisodes.Average(e => e.Duration.Ticks);
            return TimeSpan.FromTicks(avgTicks);
        }
    }

    /// <summary>
    /// Gets a copy of the assessment history.
    /// </summary>
    /// <param name="count">Maximum number of recent assessments to return.</param>
    /// <returns>Recent flow assessments ordered by time descending.</returns>
    public List<FlowAssessment> GetAssessmentHistory(int count = 50)
    {
        lock (_lock)
        {
            return _assessmentHistory
                .TakeLast(count)
                .Reverse()
                .ToList();
        }
    }

    private static FlowState DetermineChannel(double skill, double challenge)
    {
        // Map skill and challenge to the 8-channel model
        // Using thirds: low (0-0.33), moderate (0.33-0.66), high (0.66-1.0)
        var skillZone = skill < 0.33 ? 0 : skill < 0.66 ? 1 : 2;
        var challengeZone = challenge < 0.33 ? 0 : challenge < 0.66 ? 1 : 2;

        return (skillZone, challengeZone) switch
        {
            (0, 0) => FlowState.Apathy,
            (0, 1) => FlowState.Worry,
            (0, 2) => FlowState.Anxiety,
            (1, 2) => FlowState.Arousal,
            (2, 2) => FlowState.Flow,
            (2, 1) => FlowState.Control,
            (2, 0) => FlowState.Relaxation,
            (1, 0) => FlowState.Boredom,
            (1, 1) => FlowState.Control, // Moderate/moderate defaults to control
            _ => FlowState.Apathy
        };
    }
}
