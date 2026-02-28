namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Thread-safe implementation of reflective reasoning with bias detection and style analysis.
/// Manages active reasoning traces and provides post-hoc analysis capabilities.
/// </summary>
public sealed partial class MetacognitiveReasoner : IReflectiveReasoner
{
    private readonly ConcurrentDictionary<Guid, ReasoningTrace> activeTraces = new();
    private readonly ConcurrentQueue<ReasoningTrace> completedTraces = new();
    private readonly object traceLock = new();
    private Guid? _currentTraceId;

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
            _currentTraceId = trace.Id;
        }

        return trace.Id;
    }

    /// <inheritdoc/>
    public Result<int, string> AddStep(ReasoningStepType stepType, string content, string justification, params int[] dependencies)
    {
        lock (traceLock)
        {
            if (!_currentTraceId.HasValue || !activeTraces.TryGetValue(_currentTraceId.Value, out var trace))
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
            activeTraces[_currentTraceId.Value] = updatedTrace;

            return Result<int, string>.Success(stepNumber);
        }
    }

    /// <inheritdoc/>
    public Result<ReasoningTrace, string> EndTrace(string conclusion, bool success)
    {
        lock (traceLock)
        {
            if (!_currentTraceId.HasValue || !activeTraces.TryRemove(_currentTraceId.Value, out var trace))
            {
                return Result<ReasoningTrace, string>.Failure("No active reasoning trace to complete.");
            }

            var confidence = CalculateConfidence(trace, success);
            var completedTrace = trace.Complete(conclusion, confidence, success);

            completedTraces.Enqueue(completedTrace);
            _currentTraceId = null;

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
            if (_currentTraceId.HasValue && activeTraces.TryGetValue(_currentTraceId.Value, out var trace))
            {
                return Option<ReasoningTrace>.Some(trace);
            }

            return Option<ReasoningTrace>.None();
        }
    }

    /// <inheritdoc/>
    public IEnumerable<ReasoningTrace> GetHistory() => completedTraces.ToArray();

}