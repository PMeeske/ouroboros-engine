namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Partial class containing fallacy detection, bias analysis, quality scoring,
/// and thinking style calculation helpers.
/// </summary>
public sealed partial class MetacognitiveReasoner
{
    #region Private Helper Methods

    private static bool HasCircularDependencies(ReasoningTrace trace)
    {
        // Check for any step that eventually depends on itself
        foreach (var step in trace.Steps)
        {
            var visited = new HashSet<int>();
            var toCheck = new Queue<int>(step.Dependencies);

            while (toCheck.Count > 0)
            {
                var dep = toCheck.Dequeue();
                if (dep == step.StepNumber)
                {
                    return true;
                }

                if (visited.Add(dep))
                {
                    var depStep = trace.Steps.FirstOrDefault(s => s.StepNumber == dep);
                    if (depStep != null)
                    {
                        foreach (var nestedDep in depStep.Dependencies)
                        {
                            toCheck.Enqueue(nestedDep);
                        }
                    }
                }
            }
        }

        return false;
    }

    private static bool HasUnsupportedConclusion(ReasoningTrace trace)
    {
        var conclusions = trace.GetStepsByType(ReasoningStepType.Conclusion).ToList();
        return conclusions.Any(c => c.Dependencies.Count == 0);
    }

    private static bool HasInsufficientObservations(ReasoningTrace trace)
    {
        var observations = trace.GetStepsByType(ReasoningStepType.Observation).Count();
        var inferences = trace.GetStepsByType(ReasoningStepType.Inference).Count();
        return observations < 1 && inferences > 0;
    }

    private static bool HasConfirmationBiasPattern(ReasoningTrace trace)
    {
        // Check if there are hypotheses but no contradicting evidence or revisions
        var hypotheses = trace.GetStepsByType(ReasoningStepType.Hypothesis).Count();
        var contradictions = trace.GetStepsByType(ReasoningStepType.Contradiction).Count();
        var revisions = trace.GetStepsByType(ReasoningStepType.Revision).Count();

        return hypotheses > 0 && contradictions == 0 && revisions == 0;
    }

    private static bool HasHastyGeneralization(ReasoningTrace trace)
    {
        var observations = trace.GetStepsByType(ReasoningStepType.Observation).Count();
        var conclusions = trace.GetStepsByType(ReasoningStepType.Conclusion).Count();
        return observations <= 1 && conclusions > 0;
    }

    private static ImmutableList<string> DetectFallacies(ReasoningTrace trace)
    {
        var detected = new List<string>();

        foreach (var (fallacyName, detector) in FallacyDetectors)
        {
            if (detector(trace))
            {
                detected.Add(fallacyName);
            }
        }

        return detected.ToImmutableList();
    }

    private static double CalculateLogicalSoundness(ReasoningTrace trace, ImmutableList<string> fallacies)
    {
        if (!trace.IsLogicallyConsistent())
        {
            return 0.3;
        }

        var baseScore = 0.9;
        var penaltyPerFallacy = 0.15;
        var score = baseScore - (fallacies.Count * penaltyPerFallacy);

        return Math.Max(0.0, Math.Min(1.0, score));
    }

    private static double CalculateEvidenceSupport(ReasoningTrace trace)
    {
        var observations = trace.GetStepsByType(ReasoningStepType.Observation).Count();
        var inferences = trace.GetStepsByType(ReasoningStepType.Inference).Count();
        var validations = trace.GetStepsByType(ReasoningStepType.Validation).Count();

        if (observations == 0)
        {
            return 0.2;
        }

        var ratio = (double)(observations + validations) / Math.Max(1, inferences + 1);
        return Math.Min(1.0, ratio * 0.5 + 0.3);
    }

    private static double CalculateQualityScore(double logicalSoundness, double evidenceSupport, ReasoningTrace trace)
    {
        var completenessBonus = trace.WasSuccessful ? 0.1 : 0.0;
        var stepCountBonus = Math.Min(0.1, trace.Steps.Count * 0.02);

        var weightedScore = (logicalSoundness * 0.4) + (evidenceSupport * 0.4) + completenessBonus + stepCountBonus;
        return Math.Min(1.0, weightedScore);
    }

    private static double CalculateConfidence(ReasoningTrace trace, bool success)
    {
        if (!success)
        {
            return 0.1;
        }

        var observations = trace.GetStepsByType(ReasoningStepType.Observation).Count();
        var validations = trace.GetStepsByType(ReasoningStepType.Validation).Count();

        var baseConfidence = 0.5;
        var observationBonus = Math.Min(0.2, observations * 0.05);
        var validationBonus = Math.Min(0.2, validations * 0.1);

        return Math.Min(0.95, baseConfidence + observationBonus + validationBonus);
    }

    private static ImmutableList<string> IdentifyMissedConsiderations(ReasoningTrace trace)
    {
        var missed = new List<string>();

        var hasAssumptions = trace.GetStepsByType(ReasoningStepType.Assumption).Any();
        if (!hasAssumptions && trace.Steps.Count > 3)
        {
            missed.Add("Consider explicitly stating underlying assumptions.");
        }

        var hasValidation = trace.GetStepsByType(ReasoningStepType.Validation).Any();
        if (!hasValidation && trace.GetStepsByType(ReasoningStepType.Hypothesis).Any())
        {
            missed.Add("Hypotheses should be validated before being used in further reasoning.");
        }

        return missed.ToImmutableList();
    }

    private static ImmutableList<string> GenerateAlternativeConclusions(ReasoningTrace trace)
    {
        if (trace.Steps.Count < 3)
        {
            return ImmutableList<string>.Empty;
        }

        return ImmutableList.Create(
            "Consider alternative interpretations of the observations.",
            "Explore whether different assumptions would lead to different conclusions.");
    }

    private static double CalculateAnalyticalScore(IReadOnlyList<ReasoningTrace> history)
    {
        var totalInferences = history.Sum(t => t.GetStepsByType(ReasoningStepType.Inference).Count());
        var totalSteps = history.Sum(t => t.Steps.Count);
        return totalSteps > 0 ? Math.Min(1.0, (double)totalInferences / totalSteps * 2.5) : 0.5;
    }

    private static double CalculateCreativeScore(IReadOnlyList<ReasoningTrace> history)
    {
        var totalHypotheses = history.Sum(t => t.GetStepsByType(ReasoningStepType.Hypothesis).Count());
        var totalTraces = history.Count;
        return totalTraces > 0 ? Math.Min(1.0, (double)totalHypotheses / totalTraces * 0.5) : 0.5;
    }

    private static double CalculateSystematicScore(IReadOnlyList<ReasoningTrace> history)
    {
        var validTraces = history.Count(t => t.IsLogicallyConsistent());
        return history.Count > 0 ? (double)validTraces / history.Count : 0.5;
    }

    private static double CalculateIntuitiveScore(IReadOnlyList<ReasoningTrace> history)
    {
        var avgObservations = history.Count > 0
            ? history.Average(t => t.GetStepsByType(ReasoningStepType.Observation).Count())
            : 2.0;
        return Math.Max(0.0, Math.Min(1.0, 1.0 - (avgObservations * 0.2)));
    }

    private static double CalculateConfirmationBiasStrength(IReadOnlyList<ReasoningTrace> traces)
    {
        var biasedTraces = traces.Count(HasConfirmationBiasPattern);
        return traces.Count > 0 ? (double)biasedTraces / traces.Count : 0.0;
    }

    private static double CalculateAnchoringStrength(IReadOnlyList<ReasoningTrace> traces)
    {
        var anchoredCount = traces.Count(trace =>
        {
            if (trace.Steps.Count < 3)
            {
                return false;
            }

            var firstStepRefs = trace.Steps.Sum(s => s.Dependencies.Count(d => d == 1));
            var totalRefs = trace.Steps.Sum(s => s.Dependencies.Count);
            return totalRefs > 0 && (double)firstStepRefs / totalRefs > 0.5;
        });

        return traces.Count > 0 ? (double)anchoredCount / traces.Count : 0.0;
    }

    private static double CalculateHastyGeneralizationStrength(IReadOnlyList<ReasoningTrace> traces)
    {
        var hastyCount = traces.Count(HasHastyGeneralization);
        return traces.Count > 0 ? (double)hastyCount / traces.Count : 0.0;
    }

    private static double CalculateStatusQuoBiasStrength(IReadOnlyList<ReasoningTrace> traces)
    {
        var noRevisionCount = traces.Count(t =>
            t.Steps.Count > 5 &&
            t.GetStepsByType(ReasoningStepType.Revision).Count() == 0);
        return traces.Count > 0 ? (double)noRevisionCount / traces.Count : 0.0;
    }

    private static string DetermineStyleName(double analytical, double creative, double systematic, double intuitive)
    {
        var scores = new (string Name, double Score)[]
        {
            ("Analytical", analytical),
            ("Creative", creative),
            ("Systematic", systematic),
            ("Intuitive", intuitive),
        };

        var dominant = scores.MaxBy(s => s.Score);
        var secondary = scores.Where(s => s.Name != dominant.Name).MaxBy(s => s.Score);

        if (Math.Abs(dominant.Score - secondary.Score) < 0.15)
        {
            return $"Balanced ({dominant.Name}/{secondary.Name})";
        }

        return dominant.Name;
    }

    #endregion
}
