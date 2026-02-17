using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Interface for introspection operations on cognitive state.
/// </summary>
public interface IIntrospector
{
    /// <summary>
    /// Captures the current internal state as an immutable snapshot.
    /// </summary>
    /// <returns>Result containing the captured state or an error message.</returns>
    Result<InternalState, string> CaptureState();

    /// <summary>
    /// Analyzes an internal state and generates an introspection report.
    /// </summary>
    /// <param name="state">The state to analyze.</param>
    /// <returns>Result containing the report or an error message.</returns>
    Result<IntrospectionReport, string> Analyze(InternalState state);

    /// <summary>
    /// Compares two states and generates a comparison report.
    /// </summary>
    /// <param name="before">The earlier state.</param>
    /// <param name="after">The later state.</param>
    /// <returns>Result containing the comparison or an error message.</returns>
    Result<StateComparison, string> CompareStates(InternalState before, InternalState after);

    /// <summary>
    /// Identifies patterns across a history of states.
    /// </summary>
    /// <param name="history">The state history to analyze.</param>
    /// <returns>Result containing pattern observations or an error message.</returns>
    Result<ImmutableList<string>, string> IdentifyPatterns(IEnumerable<InternalState> history);

    /// <summary>
    /// Retrieves the history of captured states.
    /// </summary>
    /// <returns>Result containing the state history or an error message.</returns>
    Result<ImmutableList<InternalState>, string> GetStateHistory();

    /// <summary>
    /// Sets the current focus area.
    /// </summary>
    /// <param name="focus">The focus to set.</param>
    /// <returns>Result indicating success or an error message.</returns>
    Result<Unit, string> SetCurrentFocus(string focus);

    /// <summary>
    /// Adds a goal to the active goals list.
    /// </summary>
    /// <param name="goal">The goal to add.</param>
    /// <returns>Result indicating success or an error message.</returns>
    Result<Unit, string> AddGoal(string goal);

    /// <summary>
    /// Removes a goal from the active goals list.
    /// </summary>
    /// <param name="goal">The goal to remove.</param>
    /// <returns>Result indicating success or an error message.</returns>
    Result<Unit, string> RemoveGoal(string goal);

    /// <summary>
    /// Updates the cognitive load value.
    /// </summary>
    /// <param name="load">The cognitive load (0 to 1).</param>
    /// <returns>Result indicating success or an error message.</returns>
    Result<Unit, string> SetCognitiveLoad(double load);

    /// <summary>
    /// Updates the emotional valence.
    /// </summary>
    /// <param name="valence">The valence value (-1 to 1).</param>
    /// <returns>Result indicating success or an error message.</returns>
    Result<Unit, string> SetValence(double valence);

    /// <summary>
    /// Sets the processing mode.
    /// </summary>
    /// <param name="mode">The processing mode to set.</param>
    /// <returns>Result indicating success or an error message.</returns>
    Result<Unit, string> SetMode(ProcessingMode mode);
}