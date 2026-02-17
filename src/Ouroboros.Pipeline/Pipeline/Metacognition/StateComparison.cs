namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Represents a comparison between two internal states, tracking deltas and changes.
/// </summary>
public sealed record StateComparison(
    InternalState Before,
    InternalState After,
    double CognitiveLoadDelta,
    double ValenceDelta,
    ImmutableDictionary<string, double> AttentionChanges,
    string Interpretation)
{
    /// <summary>
    /// Creates a comparison between two states with computed deltas.
    /// </summary>
    /// <param name="before">The earlier state.</param>
    /// <param name="after">The later state.</param>
    /// <returns>A StateComparison with computed deltas.</returns>
    public static StateComparison Create(InternalState before, InternalState after)
    {
        var loadDelta = after.CognitiveLoad - before.CognitiveLoad;
        var valenceDelta = after.EmotionalValence - before.EmotionalValence;
        var attentionChanges = ComputeAttentionChanges(before.AttentionDistribution, after.AttentionDistribution);
        var interpretation = InterpretChanges(loadDelta, valenceDelta, attentionChanges, before.Mode, after.Mode);

        return new StateComparison(before, after, loadDelta, valenceDelta, attentionChanges, interpretation);
    }

    /// <summary>
    /// Time elapsed between the two states.
    /// </summary>
    public TimeSpan TimeElapsed => After.Timestamp - Before.Timestamp;

    /// <summary>
    /// Indicates whether cognitive load increased significantly.
    /// </summary>
    public bool CognitiveLoadIncreased => CognitiveLoadDelta > 0.1;

    /// <summary>
    /// Indicates whether cognitive load decreased significantly.
    /// </summary>
    public bool CognitiveLoadDecreased => CognitiveLoadDelta < -0.1;

    /// <summary>
    /// Indicates whether the processing mode changed.
    /// </summary>
    public bool ModeChanged => Before.Mode != After.Mode;

    /// <summary>
    /// Goals added between states.
    /// </summary>
    public ImmutableList<string> GoalsAdded =>
        After.ActiveGoals.Except(Before.ActiveGoals).ToImmutableList();

    /// <summary>
    /// Goals removed between states.
    /// </summary>
    public ImmutableList<string> GoalsRemoved =>
        Before.ActiveGoals.Except(After.ActiveGoals).ToImmutableList();

    private static ImmutableDictionary<string, double> ComputeAttentionChanges(
        ImmutableDictionary<string, double> before,
        ImmutableDictionary<string, double> after)
    {
        var allKeys = before.Keys.Union(after.Keys).ToHashSet();
        var builder = ImmutableDictionary.CreateBuilder<string, double>();

        foreach (var key in allKeys)
        {
            var beforeVal = before.GetValueOrDefault(key, 0.0);
            var afterVal = after.GetValueOrDefault(key, 0.0);
            var delta = afterVal - beforeVal;
            if (Math.Abs(delta) > 0.01)
            {
                builder[key] = delta;
            }
        }

        return builder.ToImmutable();
    }

    private static string InterpretChanges(
        double loadDelta,
        double valenceDelta,
        ImmutableDictionary<string, double> attentionChanges,
        ProcessingMode beforeMode,
        ProcessingMode afterMode)
    {
        var interpretations = new List<string>();

        // Cognitive load interpretation
        if (loadDelta > 0.3)
        {
            interpretations.Add("Significant cognitive load increase detected, indicating complex processing demands.");
        }
        else if (loadDelta < -0.3)
        {
            interpretations.Add("Substantial cognitive load reduction, suggesting task completion or simplification.");
        }
        else if (Math.Abs(loadDelta) > 0.1)
        {
            interpretations.Add(loadDelta > 0
                ? "Moderate cognitive load increase observed."
                : "Moderate cognitive load decrease observed.");
        }

        // Valence interpretation
        if (valenceDelta > 0.3)
        {
            interpretations.Add("Positive shift in emotional valence, indicating favorable processing outcome.");
        }
        else if (valenceDelta < -0.3)
        {
            interpretations.Add("Negative shift in emotional valence, suggesting processing difficulties or challenges.");
        }

        // Mode change interpretation
        if (beforeMode != afterMode)
        {
            interpretations.Add($"Processing mode shifted from {beforeMode} to {afterMode}.");
        }

        // Attention changes interpretation
        if (attentionChanges.Count > 3)
        {
            interpretations.Add("Multiple attention redistributions detected, indicating broad context switching.");
        }
        else if (attentionChanges.Count > 0)
        {
            var majorShift = attentionChanges.MaxBy(kvp => Math.Abs(kvp.Value));
            if (Math.Abs(majorShift.Value) > 0.2)
            {
                interpretations.Add($"Major attention shift toward '{majorShift.Key}' detected.");
            }
        }

        return interpretations.Count > 0
            ? string.Join(" ", interpretations)
            : "No significant state changes detected between snapshots.";
    }
}