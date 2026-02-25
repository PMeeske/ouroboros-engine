namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Represents a complete trace of a reasoning process from start to conclusion.
/// Immutable record capturing the full logical chain for analysis and reflection.
/// </summary>
/// <param name="Id">Unique identifier for this reasoning trace.</param>
/// <param name="StartTime">When the reasoning process began.</param>
/// <param name="EndTime">When the reasoning process concluded (null if still active).</param>
/// <param name="Steps">The ordered list of reasoning steps.</param>
/// <param name="FinalConclusion">The final conclusion reached (null if incomplete or failed).</param>
/// <param name="Confidence">The confidence level in the conclusion (0.0 to 1.0).</param>
/// <param name="WasSuccessful">Whether the reasoning process reached a valid conclusion.</param>
public sealed record ReasoningTrace(
    Guid Id,
    DateTime StartTime,
    DateTime? EndTime,
    ImmutableList<ReasoningStep> Steps,
    string? FinalConclusion,
    double Confidence,
    bool WasSuccessful)
{
    /// <summary>
    /// Creates a new reasoning trace ready to begin recording.
    /// </summary>
    /// <returns>A new empty ReasoningTrace.</returns>
    public static ReasoningTrace Start() => new(
        Id: Guid.NewGuid(),
        StartTime: DateTime.UtcNow,
        EndTime: null,
        Steps: ImmutableList<ReasoningStep>.Empty,
        FinalConclusion: null,
        Confidence: 0.0,
        WasSuccessful: false);

    /// <summary>
    /// Creates a reasoning trace with an existing identifier.
    /// </summary>
    /// <param name="id">The identifier to use.</param>
    /// <returns>A new empty ReasoningTrace with the specified ID.</returns>
    public static ReasoningTrace StartWithId(Guid id) => new(
        Id: id,
        StartTime: DateTime.UtcNow,
        EndTime: null,
        Steps: ImmutableList<ReasoningStep>.Empty,
        FinalConclusion: null,
        Confidence: 0.0,
        WasSuccessful: false);

    /// <summary>
    /// Gets the next step number for this trace.
    /// </summary>
    public int NextStepNumber => Steps.Count + 1;

    /// <summary>
    /// Gets the duration of the reasoning process if completed.
    /// </summary>
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;

    /// <summary>
    /// Gets whether this trace is still active (not yet concluded).
    /// </summary>
    public bool IsActive => !EndTime.HasValue;

    /// <summary>
    /// Adds a reasoning step to the trace.
    /// </summary>
    /// <param name="step">The step to add.</param>
    /// <returns>A new ReasoningTrace with the added step.</returns>
    public ReasoningTrace WithStep(ReasoningStep step)
        => this with { Steps = Steps.Add(step) };

    /// <summary>
    /// Adds an observation step to the trace.
    /// </summary>
    /// <param name="content">The observed content.</param>
    /// <param name="justification">Why this observation is relevant.</param>
    /// <returns>A new ReasoningTrace with the added observation.</returns>
    public ReasoningTrace AddObservation(string content, string justification)
        => WithStep(ReasoningStep.Observation(NextStepNumber, content, justification));

    /// <summary>
    /// Adds an inference step to the trace.
    /// </summary>
    /// <param name="content">The inferred content.</param>
    /// <param name="justification">The logical justification.</param>
    /// <param name="dependencies">Step numbers this inference depends on.</param>
    /// <returns>A new ReasoningTrace with the added inference.</returns>
    public ReasoningTrace AddInference(string content, string justification, params int[] dependencies)
        => WithStep(ReasoningStep.Inference(NextStepNumber, content, justification, dependencies));

    /// <summary>
    /// Adds a hypothesis step to the trace.
    /// </summary>
    /// <param name="content">The hypothesized content.</param>
    /// <param name="justification">Why this hypothesis is worth considering.</param>
    /// <param name="dependencies">Step numbers that motivated this hypothesis.</param>
    /// <returns>A new ReasoningTrace with the added hypothesis.</returns>
    public ReasoningTrace AddHypothesis(string content, string justification, params int[] dependencies)
        => WithStep(ReasoningStep.Hypothesis(NextStepNumber, content, justification, dependencies));

    /// <summary>
    /// Completes the reasoning trace with a conclusion.
    /// </summary>
    /// <param name="conclusion">The final conclusion.</param>
    /// <param name="confidence">Confidence in the conclusion (0.0 to 1.0).</param>
    /// <param name="success">Whether the reasoning was successful.</param>
    /// <returns>A completed ReasoningTrace.</returns>
    public ReasoningTrace Complete(string conclusion, double confidence, bool success = true)
    {
        var conclusionStep = ReasoningStep.Conclusion(
            NextStepNumber,
            conclusion,
            $"Concluded with {confidence:P0} confidence",
            Steps.Select(s => s.StepNumber).ToArray());

        return this with
        {
            EndTime = DateTime.UtcNow,
            Steps = Steps.Add(conclusionStep),
            FinalConclusion = conclusion,
            Confidence = Math.Clamp(confidence, 0.0, 1.0),
            WasSuccessful = success,
        };
    }

    /// <summary>
    /// Marks the reasoning trace as failed without a conclusion.
    /// </summary>
    /// <param name="reason">The reason for failure.</param>
    /// <returns>A failed ReasoningTrace.</returns>
    public ReasoningTrace Fail(string reason) => this with
    {
        EndTime = DateTime.UtcNow,
        FinalConclusion = $"Failed: {reason}",
        Confidence = 0.0,
        WasSuccessful = false,
    };

    /// <summary>
    /// Gets all steps of a specific type.
    /// </summary>
    /// <param name="stepType">The type of steps to retrieve.</param>
    /// <returns>An enumerable of matching steps.</returns>
    public IEnumerable<ReasoningStep> GetStepsByType(ReasoningStepType stepType)
        => Steps.Where(s => s.StepType == stepType);

    /// <summary>
    /// Validates the logical consistency of the trace.
    /// </summary>
    /// <returns>True if all dependencies are valid and the trace is logically consistent.</returns>
    public bool IsLogicallyConsistent()
        => Steps.All(s => s.HasValidDependencies());
}