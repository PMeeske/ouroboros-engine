using System.Runtime.CompilerServices;

namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Provides Kleisli arrows for reflective reasoning operations.
/// Enables functional composition of metacognitive analysis steps.
/// </summary>
public static class ReflectiveReasoningArrow
{
    /// <summary>
    /// Creates a step that reflects on a reasoning trace to produce a reflection result.
    /// </summary>
    /// <param name="reasoner">The reflective reasoner to use for analysis.</param>
    /// <returns>A step that transforms a reasoning trace into a reflection result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Step<ReasoningTrace, Result<ReflectionResult, string>> Reflect(IReflectiveReasoner reasoner) =>
        trace => Task.FromResult(
            trace.Steps.Count > 0
                ? Result<ReflectionResult, string>.Success(reasoner.ReflectOn(trace))
                : Result<ReflectionResult, string>.Failure("Cannot reflect on empty reasoning trace."));

    /// <summary>
    /// Creates a step that analyzes multiple reasoning traces to determine thinking style.
    /// </summary>
    /// <param name="reasoner">The reflective reasoner to use for analysis.</param>
    /// <returns>A step that transforms reasoning traces into a thinking style profile.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Step<IEnumerable<ReasoningTrace>, Result<ThinkingStyle, string>> AnalyzeStyle(IReflectiveReasoner reasoner) =>
        traces =>
        {
            var traceList = traces.ToList();
            if (traceList.Count == 0)
            {
                return Task.FromResult(Result<ThinkingStyle, string>.Failure("Cannot analyze style without reasoning history."));
            }

            // Temporarily use the provided traces for analysis
            var biases = reasoner.IdentifyBiases(traceList);
            var style = reasoner.GetThinkingStyle();

            // Merge biases from the specific traces
            var mergedStyle = biases.Aggregate(style, (s, kv) => s.WithBias(kv.Key, kv.Value));

            return Task.FromResult(Result<ThinkingStyle, string>.Success(mergedStyle));
        };

    /// <summary>
    /// Creates a step that identifies biases from reasoning history.
    /// </summary>
    /// <param name="reasoner">The reflective reasoner to use for analysis.</param>
    /// <returns>A step that identifies biases from reasoning traces.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Step<IEnumerable<ReasoningTrace>, Result<ImmutableDictionary<string, double>, string>> IdentifyBiases(IReflectiveReasoner reasoner) =>
        traces =>
        {
            var traceList = traces.ToList();
            if (traceList.Count < 3)
            {
                return Task.FromResult(Result<ImmutableDictionary<string, double>, string>.Failure(
                    "At least 3 reasoning traces are needed for reliable bias detection."));
            }

            var biases = reasoner.IdentifyBiases(traceList);
            return Task.FromResult(Result<ImmutableDictionary<string, double>, string>.Success(biases));
        };

    /// <summary>
    /// Creates a step that suggests improvements for a reasoning trace.
    /// </summary>
    /// <param name="reasoner">The reflective reasoner to use for analysis.</param>
    /// <returns>A step that generates improvement suggestions.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Step<ReasoningTrace, Result<ImmutableList<string>, string>> SuggestImprovements(IReflectiveReasoner reasoner) =>
        trace => Task.FromResult(
            trace.Steps.Count > 0
                ? Result<ImmutableList<string>, string>.Success(reasoner.SuggestImprovement(trace))
                : Result<ImmutableList<string>, string>.Failure("Cannot suggest improvements for empty trace."));

    /// <summary>
    /// Creates a composed step that performs full metacognitive analysis on a trace.
    /// Combines reflection, bias detection, and improvement suggestions.
    /// </summary>
    /// <param name="reasoner">The reflective reasoner to use.</param>
    /// <returns>A step that performs comprehensive metacognitive analysis.</returns>
    public static Step<ReasoningTrace, Result<MetacognitiveAnalysis, string>> FullAnalysis(IReflectiveReasoner reasoner) =>
        async trace =>
        {
            if (trace.Steps.Count == 0)
            {
                return Result<MetacognitiveAnalysis, string>.Failure("Cannot analyze empty reasoning trace.");
            }

            var reflection = reasoner.ReflectOn(trace);
            var improvements = reasoner.SuggestImprovement(trace);
            var style = reasoner.GetThinkingStyle();

            var analysis = new MetacognitiveAnalysis(
                Trace: trace,
                Reflection: reflection,
                Style: style,
                Improvements: improvements,
                AnalyzedAt: DateTime.UtcNow);

            return Result<MetacognitiveAnalysis, string>.Success(analysis);
        };

    /// <summary>
    /// Creates a step that validates reasoning quality against a threshold.
    /// </summary>
    /// <param name="reasoner">The reflective reasoner to use.</param>
    /// <param name="qualityThreshold">Minimum acceptable quality score.</param>
    /// <returns>A step that validates reasoning quality.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Step<ReasoningTrace, Result<bool, string>> ValidateQuality(IReflectiveReasoner reasoner, double qualityThreshold = 0.7) =>
        trace =>
        {
            if (trace.Steps.Count == 0)
            {
                return Task.FromResult(Result<bool, string>.Failure("Cannot validate empty trace."));
            }

            var reflection = reasoner.ReflectOn(trace);
            var meetsThreshold = reflection.MeetsQualityThreshold(qualityThreshold);

            return Task.FromResult(Result<bool, string>.Success(meetsThreshold));
        };
}