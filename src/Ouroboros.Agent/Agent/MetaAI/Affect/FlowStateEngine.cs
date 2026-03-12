// ==========================================================
// Flow State Engine
// Phase 3: Affective Dynamics - Csikszentmihalyi flow model
// Models 8 flow channels based on skill/challenge balance
// ==========================================================

namespace Ouroboros.Agent.MetaAI.Affect;

/// <summary>
/// The eight channels of the Csikszentmihalyi flow model,
/// determined by the relationship between skill level and challenge level.
/// </summary>
public enum FlowChannel
{
    /// <summary>Low skill, low challenge: lack of engagement.</summary>
    Apathy,

    /// <summary>Low skill, moderate challenge: unease about ability.</summary>
    Worry,

    /// <summary>Low skill, high challenge: overwhelmed by difficulty.</summary>
    Anxiety,

    /// <summary>Moderate skill, high challenge: heightened engagement approaching flow.</summary>
    Arousal,

    /// <summary>High skill, high challenge: optimal experience with full absorption.</summary>
    Flow,

    /// <summary>High skill, moderate challenge: comfortable mastery.</summary>
    Control,

    /// <summary>High skill, low challenge: effortless engagement.</summary>
    Relaxation,

    /// <summary>Moderate skill, low challenge: under-stimulation.</summary>
    Boredom
}

/// <summary>
/// Result of assessing the current flow state.
/// </summary>
/// <param name="Channel">The determined flow channel.</param>
/// <param name="SkillLevel">Current skill level (0.0 to 1.0).</param>
/// <param name="ChallengeLevel">Current challenge level (0.0 to 1.0).</param>
/// <param name="Absorption">Degree of cognitive absorption (0.0 to 1.0).</param>
/// <param name="ChallengeSkillRatio">Ratio of challenge to skill.</param>
/// <param name="IsInFlow">Whether the agent is currently in a flow state.</param>
/// <param name="TimeDistortionFactor">Perceived time dilation factor (1.0 = normal).</param>
/// <param name="Timestamp">When the assessment was made.</param>
public sealed record FlowAssessment(
    FlowChannel Channel,
    double SkillLevel,
    double ChallengeLevel,
    double Absorption,
    double ChallengeSkillRatio,
    bool IsInFlow,
    double TimeDistortionFactor,
    DateTime Timestamp);

/// <summary>
/// Recommendation for optimizing task parameters to achieve flow.
/// </summary>
/// <param name="TaskDescription">The task being optimized.</param>
/// <param name="CurrentChannel">The current flow channel.</param>
/// <param name="RecommendedChallengeAdjustment">Suggested change to challenge level (-1.0 to 1.0).</param>
/// <param name="RecommendedSkillUtilization">How much of available skill to engage (0.0 to 1.0).</param>
/// <param name="EstimatedFlowProbability">Probability of achieving flow with adjustments (0.0 to 1.0).</param>
/// <param name="Rationale">Explanation of the optimization recommendation.</param>
public sealed record FlowOptimization(
    string TaskDescription,
    FlowChannel CurrentChannel,
    double RecommendedChallengeAdjustment,
    double RecommendedSkillUtilization,
    double EstimatedFlowProbability,
    string Rationale);

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
    FlowChannel Channel,
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
    private static readonly Dictionary<FlowChannel, double> TimeDistortionFactors = new()
    {
        [FlowChannel.Apathy] = 1.8,
        [FlowChannel.Worry] = 1.3,
        [FlowChannel.Anxiety] = 1.5,
        [FlowChannel.Arousal] = 0.8,
        [FlowChannel.Flow] = 0.5,
        [FlowChannel.Control] = 0.7,
        [FlowChannel.Relaxation] = 1.0,
        [FlowChannel.Boredom] = 2.0,
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
        var isInFlow = channel == FlowChannel.Flow
                       && ratio >= 0.8 && ratio <= 1.2
                       && skillLevel > 0.5 && challengeLevel > 0.5;

        var timeDistortion = TimeDistortionFactors.GetValueOrDefault(channel, 1.0);

        // Absorption further modifies time distortion in flow
        if (isInFlow)
        {
            timeDistortion *= (1.0 - (absorption * 0.3));
        }

        var assessment = new FlowAssessment(
            Channel: channel,
            SkillLevel: skillLevel,
            ChallengeLevel: challengeLevel,
            Absorption: absorption,
            ChallengeSkillRatio: Math.Round(ratio, 3),
            IsInFlow: isInFlow,
            TimeDistortionFactor: Math.Round(timeDistortion, 3),
            Timestamp: DateTime.UtcNow);

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
            TaskDescription: taskDescription,
            CurrentChannel: currentChannel,
            RecommendedChallengeAdjustment: Math.Round(challengeAdjustment, 3),
            RecommendedSkillUtilization: skillUtilization,
            EstimatedFlowProbability: Math.Round(flowProbability, 3),
            Rationale: rationale);

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

            return (double)_assessmentHistory.Count(a => a.IsInFlow) / _assessmentHistory.Count;
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
                .Where(e => e.Channel == FlowChannel.Flow)
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
                .OrderByDescending(a => a.Timestamp)
                .Take(count)
                .ToList();
        }
    }

    private static FlowChannel DetermineChannel(double skill, double challenge)
    {
        // Map skill and challenge to the 8-channel model
        // Using thirds: low (0-0.33), moderate (0.33-0.66), high (0.66-1.0)
        var skillZone = skill < 0.33 ? 0 : skill < 0.66 ? 1 : 2;
        var challengeZone = challenge < 0.33 ? 0 : challenge < 0.66 ? 1 : 2;

        return (skillZone, challengeZone) switch
        {
            (0, 0) => FlowChannel.Apathy,
            (0, 1) => FlowChannel.Worry,
            (0, 2) => FlowChannel.Anxiety,
            (1, 2) => FlowChannel.Arousal,
            (2, 2) => FlowChannel.Flow,
            (2, 1) => FlowChannel.Control,
            (2, 0) => FlowChannel.Relaxation,
            (1, 0) => FlowChannel.Boredom,
            (1, 1) => FlowChannel.Control, // Moderate/moderate defaults to control
            _ => FlowChannel.Apathy
        };
    }
}
