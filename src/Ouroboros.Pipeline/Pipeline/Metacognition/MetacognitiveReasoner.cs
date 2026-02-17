namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Thread-safe implementation of reflective reasoning with bias detection and style analysis.
/// Manages active reasoning traces and provides post-hoc analysis capabilities.
/// </summary>
public sealed class MetacognitiveReasoner : IReflectiveReasoner
{
    private readonly ConcurrentDictionary<Guid, ReasoningTrace> activeTraces = new();
    private readonly ConcurrentQueue<ReasoningTrace> completedTraces = new();
    private readonly object traceLock = new();
    private Guid? currentTraceId;

    /// <summary>
    /// Known logical fallacies and their detection patterns.
    /// </summary>
    private static readonly ImmutableDictionary<string, Func<ReasoningTrace, bool>> FallacyDetectors =
        ImmutableDictionary<string, Func<ReasoningTrace, bool>>.Empty
            .Add("Circular Reasoning", HasCircularDependencies)
            .Add("Unsupported Conclusion", HasUnsupportedConclusion)
            .Add("Missing Evidence", HasInsufficientObservations)
            .Add("Confirmation Bias Pattern", HasConfirmationBiasPattern)
            .Add("Hasty Generalization", HasHastyGeneralization);

    /// <summary>
    /// Known cognitive biases and their detection heuristics.
    /// </summary>
    private static readonly ImmutableList<string> KnownBiases = ImmutableList.Create(
        "Confirmation Bias",
        "Anchoring Bias",
        "Availability Heuristic",
        "Dunning-Kruger Effect",
        "Hindsight Bias",
        "Status Quo Bias",
        "Sunk Cost Fallacy",
        "Bandwagon Effect");

    /// <inheritdoc/>
    public Guid StartTrace()
    {
        var trace = ReasoningTrace.Start();
        activeTraces[trace.Id] = trace;

        lock (traceLock)
        {
            currentTraceId = trace.Id;
        }

        return trace.Id;
    }

    /// <inheritdoc/>
    public Result<int, string> AddStep(ReasoningStepType stepType, string content, string justification, params int[] dependencies)
    {
        lock (traceLock)
        {
            if (!currentTraceId.HasValue || !activeTraces.TryGetValue(currentTraceId.Value, out var trace))
            {
                return Result<int, string>.Failure("No active reasoning trace. Call StartTrace() first.");
            }

            var stepNumber = trace.NextStepNumber;
            var step = new ReasoningStep(
                StepNumber: stepNumber,
                StepType: stepType,
                Content: content,
                Justification: justification,
                Timestamp: DateTime.UtcNow,
                Dependencies: dependencies.ToImmutableList());

            if (!step.HasValidDependencies() && dependencies.Length > 0)
            {
                return Result<int, string>.Failure($"Invalid dependencies: all referenced steps must exist and precede step {stepNumber}.");
            }

            var updatedTrace = trace.WithStep(step);
            activeTraces[currentTraceId.Value] = updatedTrace;

            return Result<int, string>.Success(stepNumber);
        }
    }

    /// <inheritdoc/>
    public Result<ReasoningTrace, string> EndTrace(string conclusion, bool success)
    {
        lock (traceLock)
        {
            if (!currentTraceId.HasValue || !activeTraces.TryRemove(currentTraceId.Value, out var trace))
            {
                return Result<ReasoningTrace, string>.Failure("No active reasoning trace to complete.");
            }

            var confidence = CalculateConfidence(trace, success);
            var completedTrace = trace.Complete(conclusion, confidence, success);

            completedTraces.Enqueue(completedTrace);
            currentTraceId = null;

            return Result<ReasoningTrace, string>.Success(completedTrace);
        }
    }

    /// <inheritdoc/>
    public ReflectionResult ReflectOn(ReasoningTrace trace)
    {
        if (trace.Steps.Count == 0)
        {
            return ReflectionResult.Invalid(trace);
        }

        var fallacies = DetectFallacies(trace);
        var logicalSoundness = CalculateLogicalSoundness(trace, fallacies);
        var evidenceSupport = CalculateEvidenceSupport(trace);
        var qualityScore = CalculateQualityScore(logicalSoundness, evidenceSupport, trace);
        var missedConsiderations = IdentifyMissedConsiderations(trace);
        var alternativeConclusions = GenerateAlternativeConclusions(trace);
        var improvements = SuggestImprovement(trace);

        return new ReflectionResult(
            OriginalTrace: trace,
            QualityScore: qualityScore,
            LogicalSoundness: logicalSoundness,
            EvidenceSupport: evidenceSupport,
            IdentifiedFallacies: fallacies,
            MissedConsiderations: missedConsiderations,
            AlternativeConclusions: alternativeConclusions,
            Improvements: improvements);
    }

    /// <inheritdoc/>
    public ThinkingStyle GetThinkingStyle()
    {
        var history = GetHistory().ToList();
        if (history.Count == 0)
        {
            return ThinkingStyle.Balanced();
        }

        var analyticalScore = CalculateAnalyticalScore(history);
        var creativeScore = CalculateCreativeScore(history);
        var systematicScore = CalculateSystematicScore(history);
        var intuitiveScore = CalculateIntuitiveScore(history);
        var biases = IdentifyBiases(history);

        var styleName = DetermineStyleName(analyticalScore, creativeScore, systematicScore, intuitiveScore);

        return new ThinkingStyle(
            StyleName: styleName,
            AnalyticalScore: analyticalScore,
            CreativeScore: creativeScore,
            SystematicScore: systematicScore,
            IntuitiveScore: intuitiveScore,
            BiasProfile: biases);
    }

    /// <inheritdoc/>
    public ImmutableDictionary<string, double> IdentifyBiases(IEnumerable<ReasoningTrace> history)
    {
        var traces = history.ToList();
        if (traces.Count == 0)
        {
            return ImmutableDictionary<string, double>.Empty;
        }

        var biases = ImmutableDictionary<string, double>.Empty;

        // Check for confirmation bias - tendency to only add supporting evidence
        var confirmationBiasScore = CalculateConfirmationBiasStrength(traces);
        if (confirmationBiasScore > 0.2)
        {
            biases = biases.Add("Confirmation Bias", confirmationBiasScore);
        }

        // Check for anchoring - over-reliance on initial observations
        var anchoringScore = CalculateAnchoringStrength(traces);
        if (anchoringScore > 0.2)
        {
            biases = biases.Add("Anchoring Bias", anchoringScore);
        }

        // Check for hasty generalization - jumping to conclusions with little evidence
        var hastyGenScore = CalculateHastyGeneralizationStrength(traces);
        if (hastyGenScore > 0.2)
        {
            biases = biases.Add("Hasty Generalization", hastyGenScore);
        }

        // Check for status quo bias - avoiding revisions
        var statusQuoScore = CalculateStatusQuoBiasStrength(traces);
        if (statusQuoScore > 0.2)
        {
            biases = biases.Add("Status Quo Bias", statusQuoScore);
        }

        return biases;
    }

    /// <inheritdoc/>
    public ImmutableList<string> SuggestImprovement(ReasoningTrace trace)
    {
        var suggestions = new List<string>();

        // Check observation count
        var observations = trace.GetStepsByType(ReasoningStepType.Observation).Count();
        if (observations < 2)
        {
            suggestions.Add("Gather more observations before drawing inferences.");
        }

        // Check for hypothesis testing
        var hypotheses = trace.GetStepsByType(ReasoningStepType.Hypothesis).Count();
        var validations = trace.GetStepsByType(ReasoningStepType.Validation).Count();
        if (hypotheses > 0 && validations == 0)
        {
            suggestions.Add("Validate hypotheses before accepting them as premises.");
        }

        // Check for revisions
        var revisions = trace.GetStepsByType(ReasoningStepType.Revision).Count();
        if (trace.Steps.Count > 5 && revisions == 0)
        {
            suggestions.Add("Consider revising initial assumptions as new information emerges.");
        }

        // Check dependency chains
        var isolatedSteps = trace.Steps.Where(s =>
            s.StepType != ReasoningStepType.Observation &&
            s.Dependencies.Count == 0).Count();
        if (isolatedSteps > 1)
        {
            suggestions.Add("Ensure non-observation steps reference their supporting evidence.");
        }

        // Check conclusion support
        var conclusions = trace.GetStepsByType(ReasoningStepType.Conclusion).ToList();
        if (conclusions.Count > 0)
        {
            var lastConclusion = conclusions.Last();
            if (lastConclusion.Dependencies.Count < 2)
            {
                suggestions.Add("Strengthen conclusions by explicitly referencing multiple supporting steps.");
            }
        }

        // Check for contradictions
        var contradictions = trace.GetStepsByType(ReasoningStepType.Contradiction).Count();
        if (contradictions > 0)
        {
            suggestions.Add("Resolve identified contradictions before concluding.");
        }

        // Encourage deeper analysis
        if (trace.Steps.Count < 4)
        {
            suggestions.Add("Develop reasoning more thoroughly before reaching conclusions.");
        }

        return suggestions.ToImmutableList();
    }

    /// <inheritdoc/>
    public Option<ReasoningTrace> GetActiveTrace()
    {
        lock (traceLock)
        {
            if (currentTraceId.HasValue && activeTraces.TryGetValue(currentTraceId.Value, out var trace))
            {
                return Option<ReasoningTrace>.Some(trace);
            }

            return Option<ReasoningTrace>.None();
        }
    }

    /// <inheritdoc/>
    public IEnumerable<ReasoningTrace> GetHistory() => completedTraces.ToArray();

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

    private ImmutableList<string> DetectFallacies(ReasoningTrace trace)
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
        // In a real implementation, this would analyze the evidence to suggest alternatives
        // For now, we provide generic guidance
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
        // Lower observation-to-conclusion ratio suggests more intuitive reasoning
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
        // Check if first observations are disproportionately referenced
        var anchoredCount = 0;
        foreach (var trace in traces)
        {
            if (trace.Steps.Count < 3)
            {
                continue;
            }

            var firstStepRefs = trace.Steps.Sum(s => s.Dependencies.Count(d => d == 1));
            var totalRefs = trace.Steps.Sum(s => s.Dependencies.Count);
            if (totalRefs > 0 && (double)firstStepRefs / totalRefs > 0.5)
            {
                anchoredCount++;
            }
        }

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