using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Analysis and pattern detection methods for CognitiveIntrospector.
/// </summary>
public sealed partial class CognitiveIntrospector
{
    private static IntrospectionReport AnalyzeCognitiveLoad(IntrospectionReport report, InternalState state)
    {
        report = report.WithObservation($"Cognitive load: {state.CognitiveLoad:P0}");

        if (state.CognitiveLoad > HighCognitiveLoadThreshold)
        {
            report = report
                .WithAnomaly($"High cognitive load detected ({state.CognitiveLoad:P0}).")
                .WithRecommendation("Consider reducing task complexity or delegating subtasks.");
        }
        else if (state.CognitiveLoad < LowCognitiveLoadThreshold)
        {
            report = report.WithObservation("Low cognitive load indicates available processing capacity.");
        }

        return report;
    }

    private static IntrospectionReport AnalyzeValence(IntrospectionReport report, InternalState state)
    {
        var valenceDesc = state.EmotionalValence switch
        {
            > 0.5 => "positive",
            < -0.5 => "negative",
            _ => "neutral"
        };

        report = report.WithObservation($"Emotional valence: {valenceDesc} ({state.EmotionalValence:+0.00;-0.00})");

        if (Math.Abs(state.EmotionalValence) > ValenceExtremeThreshold)
        {
            report = report
                .WithAnomaly($"Extreme emotional valence detected ({state.EmotionalValence:+0.00;-0.00}).")
                .WithRecommendation("Monitor for potential affect-cognition interference.");
        }

        return report;
    }

    private static IntrospectionReport AnalyzeAttention(IntrospectionReport report, InternalState state)
    {
        if (state.AttentionDistribution.IsEmpty)
        {
            return report.WithObservation("No attention distribution data available.");
        }

        _ = state.AttentionDistribution.Values.Sum();
        report = report.WithObservation($"Attention distributed across {state.AttentionDistribution.Count} areas.");

        // Check for attention fragmentation
        if (state.AttentionDistribution.Count > 5)
        {
            report = report
                .WithAnomaly("Attention fragmentation detected across multiple areas.")
                .WithRecommendation("Consider consolidating focus to fewer priority areas.");
        }

        // Check for attention drift (no dominant focus)
        var maxAttention = state.AttentionDistribution.Values.DefaultIfEmpty(0).Max();
        if (maxAttention < AttentionDriftThreshold && state.AttentionDistribution.Count > 1)
        {
            report = report
                .WithAnomaly("Attention drift detected - no dominant focus area.")
                .WithRecommendation("Establish clearer priority focus to improve processing efficiency.");
        }

        return report;
    }

    private static IntrospectionReport AnalyzeWorkingMemory(IntrospectionReport report, InternalState state)
    {
        var itemCount = state.WorkingMemoryItems.Count;
        report = report.WithObservation($"Working memory contains {itemCount} items.");

        if (itemCount > 7)
        {
            report = report
                .WithAnomaly($"Working memory approaching capacity ({itemCount} items).")
                .WithRecommendation("Consider chunking information or offloading to external memory.");
        }
        else if (itemCount == 0)
        {
            report = report.WithObservation("Working memory is empty, ready for new information.");
        }

        return report;
    }

    private static IntrospectionReport AnalyzeGoals(IntrospectionReport report, InternalState state)
    {
        var goalCount = state.ActiveGoals.Count;
        report = report.WithObservation($"Tracking {goalCount} active goals.");

        if (goalCount > 5)
        {
            report = report
                .WithAnomaly($"Multiple concurrent goals ({goalCount}) may cause priority conflicts.")
                .WithRecommendation("Consider prioritizing or sequencing goals to reduce cognitive overhead.");
        }
        else if (goalCount == 0)
        {
            report = report
                .WithAnomaly("No active goals detected.")
                .WithRecommendation("Establish goals to provide direction for processing.");
        }

        return report;
    }

    private static IntrospectionReport AnalyzeMode(IntrospectionReport report, InternalState state)
    {
        var modeDesc = state.Mode switch
        {
            ProcessingMode.Analytical => "systematic logical analysis",
            ProcessingMode.Creative => "generative exploration",
            ProcessingMode.Reactive => "stimulus-driven response",
            ProcessingMode.Reflective => "meta-cognitive reflection",
            ProcessingMode.Intuitive => "pattern-based recognition",
            _ => "unknown processing"
        };

        report = report.WithObservation($"Current processing mode: {state.Mode} ({modeDesc}).");

        // Mode-specific recommendations
        if (state.Mode == ProcessingMode.Reactive && state.CognitiveLoad > 0.5)
        {
            report = report.WithRecommendation(
                "High load in reactive mode - consider shifting to analytical mode for deliberate processing.");
        }

        return report;
    }

    private static double CalculateSelfAssessmentScore(InternalState state, IntrospectionReport report)
    {
        var score = 0.5; // Baseline

        // Cognitive load impact (optimal around 0.4-0.6)
        var loadDeviation = Math.Abs(state.CognitiveLoad - 0.5);
        score -= loadDeviation * 0.2;

        // Anomaly penalty
        score -= report.Anomalies.Count * 0.1;

        // Goal presence bonus
        if (state.ActiveGoals.Count > 0 && state.ActiveGoals.Count <= 3)
        {
            score += 0.1;
        }

        // Focus clarity bonus
        if (state.CurrentFocus != "None")
        {
            score += 0.05;
        }

        // Mode appropriateness (reflective mode in introspection is good)
        if (state.Mode == ProcessingMode.Reflective)
        {
            score += 0.1;
        }

        return Math.Clamp(score, 0.0, 1.0);
    }

    private static TrendDirection DetectTrend(List<double> values)
    {
        if (values.Count < 3)
        {
            return TrendDirection.Stable;
        }

        var increases = 0;
        var decreases = 0;

        for (var i = 1; i < values.Count; i++)
        {
            var delta = values[i] - values[i - 1];
            if (delta > 0.05)
            {
                increases++;
            }
            else if (delta < -0.05)
            {
                decreases++;
            }
        }

        var threshold = values.Count / 3;
        if (increases > threshold && increases > decreases * 2)
        {
            return TrendDirection.Increasing;
        }

        if (decreases > threshold && decreases > increases * 2)
        {
            return TrendDirection.Decreasing;
        }

        return TrendDirection.Stable;
    }

    private static bool DetectOscillation(List<double> values)
    {
        if (values.Count < 4)
        {
            return false;
        }

        var signChanges = 0;
        for (var i = 2; i < values.Count; i++)
        {
            var prevDelta = values[i - 1] - values[i - 2];
            var currDelta = values[i] - values[i - 1];
            if (Math.Sign(prevDelta) != Math.Sign(currDelta) && Math.Abs(prevDelta) > 0.1 && Math.Abs(currDelta) > 0.1)
            {
                signChanges++;
            }
        }

        return signChanges >= values.Count / 3;
    }

    private static int CountModeChanges(List<InternalState> states)
    {
        var changes = 0;
        for (var i = 1; i < states.Count; i++)
        {
            if (states[i].Mode != states[i - 1].Mode)
            {
                changes++;
            }
        }

        return changes;
    }

    private static int CountFocusChanges(List<InternalState> states)
    {
        var changes = 0;
        for (var i = 1; i < states.Count; i++)
        {
            if (states[i].CurrentFocus != states[i - 1].CurrentFocus)
            {
                changes++;
            }
        }

        return changes;
    }

    private static string AnalyzeGoalPatterns(List<InternalState> states)
    {
        if (states.Count < 2)
        {
            return string.Empty;
        }

        var firstGoals = states[0].ActiveGoals.ToHashSet();
        var lastGoals = states[^1].ActiveGoals.ToHashSet();

        var completed = firstGoals.Except(lastGoals).Count();
        var added = lastGoals.Except(firstGoals).Count();

        if (completed > 0 && added > 0)
        {
            return $"Goal dynamics: {completed} goals completed, {added} new goals added during observation period.";
        }

        if (completed > 0)
        {
            return $"Goal completion detected: {completed} goals achieved during observation period.";
        }

        if (added > 0)
        {
            return $"Goal expansion: {added} new goals emerged during observation period.";
        }

        return string.Empty;
    }

    private enum TrendDirection
    {
        Stable,
        Increasing,
        Decreasing
    }
}
