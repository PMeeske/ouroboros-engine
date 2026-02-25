// <copyright file="ReflectiveReasoning.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Metacognition;

using System.Collections.Immutable;
using Ouroboros.Core.Monads;

/// <summary>
/// Represents a single step in a reasoning process.
/// Captures the content, justification, and dependencies of a logical step.
/// </summary>
/// <param name="StepNumber">Sequential number of this step in the trace.</param>
/// <param name="StepType">The type of reasoning operation performed.</param>
/// <param name="Content">The actual content or claim of this reasoning step.</param>
/// <param name="Justification">The rationale or supporting argument for this step.</param>
/// <param name="Timestamp">When this step was performed.</param>
/// <param name="Dependencies">References to earlier step numbers this step depends on.</param>
public sealed record ReasoningStep(
    int StepNumber,
    ReasoningStepType StepType,
    string Content,
    string Justification,
    DateTime Timestamp,
    ImmutableList<int> Dependencies)
{
    /// <summary>
    /// Creates an observation step with no dependencies.
    /// </summary>
    /// <param name="stepNumber">The step number in the trace.</param>
    /// <param name="content">The observed content.</param>
    /// <param name="justification">Why this observation is relevant.</param>
    /// <returns>A new observation reasoning step.</returns>
    public static ReasoningStep Observation(int stepNumber, string content, string justification) => new(
        StepNumber: stepNumber,
        StepType: ReasoningStepType.Observation,
        Content: content,
        Justification: justification,
        Timestamp: DateTime.UtcNow,
        Dependencies: ImmutableList<int>.Empty);

    /// <summary>
    /// Creates an inference step from prior steps.
    /// </summary>
    /// <param name="stepNumber">The step number in the trace.</param>
    /// <param name="content">The inferred content.</param>
    /// <param name="justification">The logical justification for the inference.</param>
    /// <param name="dependencies">Step numbers this inference depends on.</param>
    /// <returns>A new inference reasoning step.</returns>
    public static ReasoningStep Inference(int stepNumber, string content, string justification, params int[] dependencies) => new(
        StepNumber: stepNumber,
        StepType: ReasoningStepType.Inference,
        Content: content,
        Justification: justification,
        Timestamp: DateTime.UtcNow,
        Dependencies: dependencies.ToImmutableList());

    /// <summary>
    /// Creates a hypothesis step.
    /// </summary>
    /// <param name="stepNumber">The step number in the trace.</param>
    /// <param name="content">The hypothesized content.</param>
    /// <param name="justification">Why this hypothesis is worth considering.</param>
    /// <param name="dependencies">Step numbers that motivated this hypothesis.</param>
    /// <returns>A new hypothesis reasoning step.</returns>
    public static ReasoningStep Hypothesis(int stepNumber, string content, string justification, params int[] dependencies) => new(
        StepNumber: stepNumber,
        StepType: ReasoningStepType.Hypothesis,
        Content: content,
        Justification: justification,
        Timestamp: DateTime.UtcNow,
        Dependencies: dependencies.ToImmutableList());

    /// <summary>
    /// Creates a conclusion step from prior reasoning.
    /// </summary>
    /// <param name="stepNumber">The step number in the trace.</param>
    /// <param name="content">The concluded content.</param>
    /// <param name="justification">How this conclusion was reached.</param>
    /// <param name="dependencies">Step numbers supporting this conclusion.</param>
    /// <returns>A new conclusion reasoning step.</returns>
    public static ReasoningStep Conclusion(int stepNumber, string content, string justification, params int[] dependencies) => new(
        StepNumber: stepNumber,
        StepType: ReasoningStepType.Conclusion,
        Content: content,
        Justification: justification,
        Timestamp: DateTime.UtcNow,
        Dependencies: dependencies.ToImmutableList());

    /// <summary>
    /// Adds a dependency to this step.
    /// </summary>
    /// <param name="stepNumber">The step number to depend on.</param>
    /// <returns>A new ReasoningStep with the added dependency.</returns>
    public ReasoningStep WithDependency(int stepNumber)
        => this with { Dependencies = Dependencies.Add(stepNumber) };

    /// <summary>
    /// Validates that all dependencies reference earlier steps.
    /// </summary>
    /// <returns>True if all dependencies are valid (reference earlier steps).</returns>
    public bool HasValidDependencies()
        => Dependencies.All(d => d > 0 && d < StepNumber);
}