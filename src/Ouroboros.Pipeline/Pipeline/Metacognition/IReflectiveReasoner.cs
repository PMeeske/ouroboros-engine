namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Interface for a system capable of reflecting on its own reasoning processes.
/// Defines the contract for metacognitive reasoning capabilities.
/// </summary>
public interface IReflectiveReasoner
{
    /// <summary>
    /// Begins recording a new reasoning trace.
    /// </summary>
    /// <returns>The ID of the newly started trace.</returns>
    Guid StartTrace();

    /// <summary>
    /// Adds a step to the currently active reasoning trace.
    /// </summary>
    /// <param name="stepType">The type of reasoning step.</param>
    /// <param name="content">The content of the step.</param>
    /// <param name="justification">The justification for this step.</param>
    /// <param name="dependencies">Step numbers this step depends on.</param>
    /// <returns>Result indicating success or failure with error message.</returns>
    Result<int, string> AddStep(ReasoningStepType stepType, string content, string justification, params int[] dependencies);

    /// <summary>
    /// Completes the active reasoning trace with a conclusion.
    /// </summary>
    /// <param name="conclusion">The final conclusion.</param>
    /// <param name="success">Whether the reasoning was successful.</param>
    /// <returns>The completed reasoning trace, or an error.</returns>
    Result<ReasoningTrace, string> EndTrace(string conclusion, bool success);

    /// <summary>
    /// Reflects on a completed reasoning trace, analyzing its quality.
    /// </summary>
    /// <param name="trace">The trace to analyze.</param>
    /// <returns>The reflection result.</returns>
    ReflectionResult ReflectOn(ReasoningTrace trace);

    /// <summary>
    /// Analyzes reasoning history to determine thinking style characteristics.
    /// </summary>
    /// <returns>The analyzed thinking style profile.</returns>
    ThinkingStyle GetThinkingStyle();

    /// <summary>
    /// Identifies potential biases from a history of reasoning traces.
    /// </summary>
    /// <param name="history">Collection of past reasoning traces to analyze.</param>
    /// <returns>Map of identified biases to their estimated strength.</returns>
    ImmutableDictionary<string, double> IdentifyBiases(IEnumerable<ReasoningTrace> history);

    /// <summary>
    /// Suggests specific improvements for a reasoning trace.
    /// </summary>
    /// <param name="trace">The trace to improve.</param>
    /// <returns>List of improvement suggestions.</returns>
    ImmutableList<string> SuggestImprovement(ReasoningTrace trace);

    /// <summary>
    /// Gets the currently active trace, if any.
    /// </summary>
    /// <returns>The active trace or None if no trace is active.</returns>
    Option<ReasoningTrace> GetActiveTrace();

    /// <summary>
    /// Gets all completed traces in the reasoner's history.
    /// </summary>
    /// <returns>Enumerable of completed traces.</returns>
    IEnumerable<ReasoningTrace> GetHistory();
}