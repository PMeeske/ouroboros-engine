// ==========================================================
// Social Emotion Processing Engine
// Phase 3: Affective Dynamics - Social emotion modeling
// Implements Gross's Process Model for emotion regulation
// ==========================================================

namespace Ouroboros.Agent.MetaAI.Affect;

/// <summary>
/// Types of social emotions modeled by the engine.
/// </summary>
public enum SocialEmotionType
{
    /// <summary>Guilt from perceived harm to others.</summary>
    Guilt,

    /// <summary>Shame from perceived failure or exposure.</summary>
    Shame,

    /// <summary>Pride from achievement or accomplishment.</summary>
    Pride,

    /// <summary>Gratitude from receiving help or kindness.</summary>
    Gratitude,

    /// <summary>Empathy from perceiving another's emotional state.</summary>
    Empathy,

    /// <summary>Jealousy from perceived unfairness or envy.</summary>
    Jealousy,

    /// <summary>Compassion from witnessing suffering.</summary>
    Compassion
}

/// <summary>
/// Emotion regulation strategies based on Gross's Process Model.
/// </summary>
public enum RegulationStrategy
{
    /// <summary>Cognitive reappraisal: reinterpreting the meaning of the situation.</summary>
    Reappraisal,

    /// <summary>Expressive suppression: inhibiting outward expression.</summary>
    Suppression,

    /// <summary>Distraction: redirecting attention away from the emotional stimulus.</summary>
    Distraction,

    /// <summary>Situation modification: changing the situation itself.</summary>
    SituationModification,

    /// <summary>Attention deployment: selectively directing attention.</summary>
    AttentionDeployment
}

/// <summary>
/// Represents a social emotion with trigger context and temporal information.
/// </summary>
/// <param name="Id">Unique identifier for this emotion instance.</param>
/// <param name="Type">The type of social emotion.</param>
/// <param name="Intensity">Current intensity level (0.0 to 1.0).</param>
/// <param name="Trigger">Description of the triggering situation.</param>
/// <param name="Context">Contextual information about other agents involved.</param>
/// <param name="Timestamp">When this emotion was generated.</param>
public sealed record SocialEmotion(
    string Id,
    SocialEmotionType Type,
    double Intensity,
    string Trigger,
    string Context,
    DateTime Timestamp);

/// <summary>
/// Result of applying an emotion regulation strategy.
/// </summary>
/// <param name="OriginalEmotion">The emotion type that was regulated.</param>
/// <param name="OriginalIntensity">Intensity before regulation.</param>
/// <param name="RegulatedIntensity">Intensity after regulation.</param>
/// <param name="UsedStrategy">The regulation strategy that was applied.</param>
/// <param name="Success">Whether the regulation reduced intensity below the threshold.</param>
public sealed record EmotionRegulationResult(
    SocialEmotionType OriginalEmotion,
    double OriginalIntensity,
    double RegulatedIntensity,
    RegulationStrategy UsedStrategy,
    bool Success);

/// <summary>
/// Result of generating an empathic response to another agent's perceived emotion.
/// </summary>
/// <param name="TargetAgent">Identifier of the agent whose emotion was perceived.</param>
/// <param name="PerceivedEmotion">The emotion detected in the other agent.</param>
/// <param name="ResonanceStrength">How strongly the empathy resonates (0.0 to 1.0).</param>
/// <param name="SuggestedResponse">A suggested empathetic response phrase.</param>
public sealed record EmpathyResponse(
    string TargetAgent,
    string PerceivedEmotion,
    double ResonanceStrength,
    string SuggestedResponse);

/// <summary>
/// Social emotion processing engine.
/// Models guilt, shame, pride, gratitude, empathy, jealousy, and compassion
/// with trigger conditions and Gross's Process Model regulation.
/// </summary>
public sealed class SocialEmotionEngine
{
    private readonly List<SocialEmotion> _activeEmotions = [];
    private readonly List<(string Id, bool WasAppropriate)> _outcomes = [];
    private readonly object _lock = new();

    /// <summary>
    /// Regulation effectiveness matrix mapping (strategy, emotion) pairs to effectiveness scores.
    /// Higher values mean the strategy is more effective at reducing that emotion type.
    /// </summary>
    private static readonly Dictionary<(RegulationStrategy, SocialEmotionType), double> EffectivenessMatrix = new()
    {
        [(RegulationStrategy.Reappraisal, SocialEmotionType.Guilt)] = 0.8,
        [(RegulationStrategy.Reappraisal, SocialEmotionType.Shame)] = 0.7,
        [(RegulationStrategy.Reappraisal, SocialEmotionType.Jealousy)] = 0.6,
        [(RegulationStrategy.Reappraisal, SocialEmotionType.Pride)] = 0.3,
        [(RegulationStrategy.Reappraisal, SocialEmotionType.Compassion)] = 0.4,
        [(RegulationStrategy.Suppression, SocialEmotionType.Guilt)] = 0.3,
        [(RegulationStrategy.Suppression, SocialEmotionType.Shame)] = 0.4,
        [(RegulationStrategy.Suppression, SocialEmotionType.Jealousy)] = 0.35,
        [(RegulationStrategy.Distraction, SocialEmotionType.Jealousy)] = 0.5,
        [(RegulationStrategy.Distraction, SocialEmotionType.Shame)] = 0.45,
        [(RegulationStrategy.Distraction, SocialEmotionType.Guilt)] = 0.35,
        [(RegulationStrategy.SituationModification, SocialEmotionType.Guilt)] = 0.75,
        [(RegulationStrategy.SituationModification, SocialEmotionType.Jealousy)] = 0.6,
        [(RegulationStrategy.AttentionDeployment, SocialEmotionType.Guilt)] = 0.4,
        [(RegulationStrategy.AttentionDeployment, SocialEmotionType.Shame)] = 0.5,
        [(RegulationStrategy.AttentionDeployment, SocialEmotionType.Jealousy)] = 0.45,
    };

    /// <summary>
    /// Evaluates a social situation and generates an appropriate emotional response.
    /// </summary>
    /// <param name="situation">Description of the social situation to evaluate.</param>
    /// <param name="otherAgentState">Perceived emotional or behavioral state of the other agent.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The generated social emotion or an error description.</returns>
    public Task<Result<SocialEmotion, string>> EvaluateSocialSituationAsync(
        string situation,
        string otherAgentState,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(situation);
        ArgumentNullException.ThrowIfNull(otherAgentState);

        if (ct.IsCancellationRequested)
            return Task.FromResult(Result<SocialEmotion, string>.Failure("Operation was cancelled."));

        var emotionType = ClassifySituation(situation, otherAgentState);
        var intensity = ComputeIntensity(situation, otherAgentState);

        var emotion = new SocialEmotion(
            Id: Guid.NewGuid().ToString("N"),
            Type: emotionType,
            Intensity: Math.Clamp(intensity, 0.0, 1.0),
            Trigger: situation,
            Context: otherAgentState,
            Timestamp: DateTime.UtcNow);

        lock (_lock)
        {
            _activeEmotions.Add(emotion);
        }

        PruneExpiredEmotions();

        return Task.FromResult(Result<SocialEmotion, string>.Success(emotion));
    }

    /// <summary>
    /// Regulates an active emotion using the specified Gross Process Model strategy.
    /// </summary>
    /// <param name="emotion">The emotion to regulate.</param>
    /// <param name="strategy">The regulation strategy to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The regulation result including before/after intensities.</returns>
    public Task<Result<EmotionRegulationResult, string>> RegulateEmotionAsync(
        SocialEmotion emotion,
        RegulationStrategy strategy,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(emotion);

        if (ct.IsCancellationRequested)
            return Task.FromResult(Result<EmotionRegulationResult, string>.Failure("Operation was cancelled."));

        var effectivenessKey = (strategy, emotion.Type);
        var effectiveness = EffectivenessMatrix.GetValueOrDefault(effectivenessKey, 0.25);

        var regulatedIntensity = emotion.Intensity * (1.0 - effectiveness);

        var result = new EmotionRegulationResult(
            OriginalEmotion: emotion.Type,
            OriginalIntensity: emotion.Intensity,
            RegulatedIntensity: Math.Clamp(regulatedIntensity, 0.0, 1.0),
            UsedStrategy: strategy,
            Success: regulatedIntensity < 0.5);

        lock (_lock)
        {
            var idx = _activeEmotions.FindIndex(e => e.Id == emotion.Id);
            if (idx >= 0)
            {
                _activeEmotions[idx] = emotion with { Intensity = Math.Clamp(regulatedIntensity, 0.0, 1.0) };
            }
        }

        return Task.FromResult(Result<EmotionRegulationResult, string>.Success(result));
    }

    /// <summary>
    /// Generates an empathic response based on the perceived emotion of another agent.
    /// </summary>
    /// <param name="targetAgentId">Identifier of the agent whose emotion is being perceived.</param>
    /// <param name="otherAgentEmotion">The perceived emotion description.</param>
    /// <param name="context">Additional context about the interaction.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An empathy response with resonance strength and suggested phrase.</returns>
    public Task<Result<EmpathyResponse, string>> GenerateEmpathyAsync(
        string targetAgentId,
        string otherAgentEmotion,
        string context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(targetAgentId);
        ArgumentNullException.ThrowIfNull(otherAgentEmotion);

        if (ct.IsCancellationRequested)
            return Task.FromResult(Result<EmpathyResponse, string>.Failure("Operation was cancelled."));

        var resonance = ComputeEmpathyResonance(otherAgentEmotion);

        var response = new EmpathyResponse(
            TargetAgent: targetAgentId,
            PerceivedEmotion: otherAgentEmotion,
            ResonanceStrength: resonance,
            SuggestedResponse: GenerateEmpatheticPhrase(otherAgentEmotion, resonance));

        return Task.FromResult(Result<EmpathyResponse, string>.Success(response));
    }

    /// <summary>
    /// Gets a snapshot of all currently active social emotions.
    /// </summary>
    /// <returns>A copy of the active emotions list.</returns>
    public List<SocialEmotion> GetActiveEmotions()
    {
        lock (_lock)
        {
            return [.. _activeEmotions];
        }
    }

    /// <summary>
    /// Records whether an emotional response was contextually appropriate,
    /// used for calibrating future emotion generation.
    /// </summary>
    /// <param name="emotionId">The identifier of the emotion to evaluate.</param>
    /// <param name="wasAppropriate">Whether the emotion was appropriate for the situation.</param>
    public void RecordEmotionOutcome(string emotionId, bool wasAppropriate)
    {
        ArgumentNullException.ThrowIfNull(emotionId);
        lock (_lock)
        {
            _outcomes.Add((emotionId, wasAppropriate));
        }
    }

    /// <summary>
    /// Gets the appropriateness rate for recorded emotion outcomes.
    /// </summary>
    /// <returns>Fraction of emotions deemed appropriate (0.0 to 1.0), or 1.0 if no outcomes recorded.</returns>
    public double GetAppropriatenessRate()
    {
        lock (_lock)
        {
            if (_outcomes.Count == 0)
                return 1.0;

            return (double)_outcomes.Count(o => o.WasAppropriate) / _outcomes.Count;
        }
    }

    private static SocialEmotionType ClassifySituation(string situation, string otherState)
    {
        var combined = $"{situation} {otherState}".ToLowerInvariant();

        if (combined.Contains("hurt") || combined.Contains("harm") || combined.Contains("damage"))
            return SocialEmotionType.Guilt;
        if (combined.Contains("fail") || combined.Contains("embarrass") || combined.Contains("exposed"))
            return SocialEmotionType.Shame;
        if (combined.Contains("achiev") || combined.Contains("success") || combined.Contains("accomplish"))
            return SocialEmotionType.Pride;
        if (combined.Contains("help") || combined.Contains("gift") || combined.Contains("thank"))
            return SocialEmotionType.Gratitude;
        if (combined.Contains("sad") || combined.Contains("pain") || combined.Contains("suffer"))
            return SocialEmotionType.Empathy;
        if (combined.Contains("jealous") || combined.Contains("envy") || combined.Contains("unfair"))
            return SocialEmotionType.Jealousy;

        return SocialEmotionType.Compassion;
    }

    private static double ComputeIntensity(string situation, string otherState)
    {
        var baseIntensity = Math.Min(situation.Length / 200.0, 0.8);
        var strongWords = new[] { "terrible", "amazing", "deeply", "extremely", "very", "incredible", "devastating" };
        var boost = strongWords.Count(w => situation.Contains(w, StringComparison.OrdinalIgnoreCase)) * 0.1;
        var contextBoost = string.IsNullOrWhiteSpace(otherState) ? 0.0 : 0.05;
        return Math.Clamp(baseIntensity + boost + contextBoost, 0.1, 1.0);
    }

    private static double ComputeEmpathyResonance(string emotion)
    {
        if (string.IsNullOrWhiteSpace(emotion))
            return 0.2;

        return Math.Min(0.3 + (emotion.Length / 100.0), 0.9);
    }

    private static string GenerateEmpatheticPhrase(string emotion, double resonance)
    {
        if (resonance > 0.7)
            return $"I can feel how {emotion} you are. I'm here with you.";
        if (resonance > 0.4)
            return $"I understand you're feeling {emotion}. That matters.";

        return $"I notice you seem {emotion}. Would you like to talk about it?";
    }

    private void PruneExpiredEmotions()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-30);
        lock (_lock)
        {
            _activeEmotions.RemoveAll(e => e.Timestamp < cutoff && e.Intensity < 0.2);
        }
    }
}
