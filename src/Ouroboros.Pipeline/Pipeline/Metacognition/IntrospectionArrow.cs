using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Provides Kleisli arrow combinators for introspection operations.
/// </summary>
public static class IntrospectionArrow
{
    /// <summary>
    /// Creates an arrow that captures the current cognitive state.
    /// </summary>
    /// <param name="introspector">The introspector to use.</param>
    /// <returns>A step that captures state.</returns>
    public static Step<Unit, Result<InternalState, string>> CaptureStateArrow(IIntrospector introspector) =>
        _ => Task.FromResult(introspector.CaptureState());

    /// <summary>
    /// Creates an arrow that analyzes an internal state.
    /// </summary>
    /// <param name="introspector">The introspector to use.</param>
    /// <returns>A step that analyzes state.</returns>
    public static Step<InternalState, Result<IntrospectionReport, string>> AnalyzeArrow(IIntrospector introspector) =>
        state => Task.FromResult(introspector.Analyze(state));

    /// <summary>
    /// Creates an arrow that compares two states.
    /// </summary>
    /// <param name="introspector">The introspector to use.</param>
    /// <returns>A step that compares states.</returns>
    public static Step<(InternalState Before, InternalState After), Result<StateComparison, string>> CompareArrow(IIntrospector introspector) =>
        pair => Task.FromResult(introspector.CompareStates(pair.Before, pair.After));

    /// <summary>
    /// Creates an arrow that identifies patterns in state history.
    /// </summary>
    /// <param name="introspector">The introspector to use.</param>
    /// <returns>A step that identifies patterns.</returns>
    public static Step<IEnumerable<InternalState>, Result<ImmutableList<string>, string>> IdentifyPatternsArrow(IIntrospector introspector) =>
        history => Task.FromResult(introspector.IdentifyPatterns(history));

    /// <summary>
    /// Creates a composed arrow that captures state and then analyzes it.
    /// </summary>
    /// <param name="introspector">The introspector to use.</param>
    /// <returns>A step that captures and analyzes state.</returns>
    public static Step<Unit, Result<IntrospectionReport, string>> FullIntrospectionArrow(IIntrospector introspector) =>
        async _ =>
        {
            var captureResult = introspector.CaptureState();
            if (!captureResult.IsSuccess)
            {
                return Result<IntrospectionReport, string>.Failure(captureResult.Error);
            }

            return introspector.Analyze(captureResult.Value);
        };

    /// <summary>
    /// Creates an arrow that sets focus and then captures state.
    /// </summary>
    /// <param name="introspector">The introspector to use.</param>
    /// <returns>A step that sets focus and captures state.</returns>
    public static Step<string, Result<InternalState, string>> FocusAndCaptureArrow(IIntrospector introspector) =>
        focus =>
        {
            var focusResult = introspector.SetCurrentFocus(focus);
            if (!focusResult.IsSuccess)
            {
                return Task.FromResult(Result<InternalState, string>.Failure(focusResult.Error));
            }

            return Task.FromResult(introspector.CaptureState());
        };

    /// <summary>
    /// Creates an arrow that performs introspection with pattern analysis.
    /// </summary>
    /// <param name="introspector">The introspector to use.</param>
    /// <returns>A step that captures state, analyzes it, and identifies patterns.</returns>
    public static Step<Unit, Result<(IntrospectionReport Report, ImmutableList<string> Patterns), string>> DeepIntrospectionArrow(IIntrospector introspector) =>
        async _ =>
        {
            // Capture current state
            var captureResult = introspector.CaptureState();
            if (!captureResult.IsSuccess)
            {
                return Result<(IntrospectionReport, ImmutableList<string>), string>.Failure(captureResult.Error);
            }

            // Analyze the captured state
            var analyzeResult = introspector.Analyze(captureResult.Value);
            if (!analyzeResult.IsSuccess)
            {
                return Result<(IntrospectionReport, ImmutableList<string>), string>.Failure(analyzeResult.Error);
            }

            // Get history and identify patterns
            var historyResult = introspector.GetStateHistory();
            if (!historyResult.IsSuccess)
            {
                return Result<(IntrospectionReport, ImmutableList<string>), string>.Failure(historyResult.Error);
            }

            var patternsResult = introspector.IdentifyPatterns(historyResult.Value);
            if (!patternsResult.IsSuccess)
            {
                return Result<(IntrospectionReport, ImmutableList<string>), string>.Failure(patternsResult.Error);
            }

            return Result<(IntrospectionReport, ImmutableList<string>), string>.Success((analyzeResult.Value, patternsResult.Value));
        };

    /// <summary>
    /// Creates an arrow that monitors state transitions over time.
    /// </summary>
    /// <param name="introspector">The introspector to use.</param>
    /// <returns>A step that compares current state with previous state if available.</returns>
    public static Step<Unit, Result<Option<StateComparison>, string>> MonitorTransitionArrow(IIntrospector introspector) =>
        _ =>
        {
            var historyResult = introspector.GetStateHistory();
            if (!historyResult.IsSuccess)
            {
                return Task.FromResult(Result<Option<StateComparison>, string>.Failure(historyResult.Error));
            }

            var history = historyResult.Value;
            if (history.Count < 2)
            {
                return Task.FromResult(Result<Option<StateComparison>, string>.Success(Option<StateComparison>.None()));
            }

            var before = history[^2];
            var after = history[^1];
            var comparisonResult = introspector.CompareStates(before, after);

            return Task.FromResult(comparisonResult.IsSuccess
                ? Result<Option<StateComparison>, string>.Success(Option<StateComparison>.Some(comparisonResult.Value))
                : Result<Option<StateComparison>, string>.Failure(comparisonResult.Error));
        };
}