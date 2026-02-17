// <copyright file="OnlineLearning.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Learning;

using System.Collections.Immutable;
using Ouroboros.Core.Monads;

/// <summary>
/// Represents immutable feedback data for online learning.
/// Captures the complete context of a learning signal including source, quality rating, and metadata.
/// </summary>
/// <param name="Id">Unique identifier for this feedback instance.</param>
/// <param name="SourceId">Identifier of the component or model that produced the output.</param>
/// <param name="InputContext">The input context that led to the output being evaluated.</param>
/// <param name="Output">The actual output that was produced and is being evaluated.</param>
/// <param name="Score">Quality rating in the range [-1, 1] where -1 is worst and 1 is best.</param>
/// <param name="Type">The type of feedback provided.</param>
/// <param name="Timestamp">When this feedback was recorded.</param>
/// <param name="Tags">Categorical tags for organizing and filtering feedback.</param>
public sealed record Feedback(
    Guid Id,
    string SourceId,
    string InputContext,
    string Output,
    double Score,
    FeedbackType Type,
    DateTime Timestamp,
    ImmutableList<string> Tags)
{
    /// <summary>
    /// Creates explicit feedback with a user-provided score.
    /// </summary>
    /// <param name="sourceId">Identifier of the producing component.</param>
    /// <param name="inputContext">The input context.</param>
    /// <param name="output">The produced output.</param>
    /// <param name="score">Quality score in [-1, 1].</param>
    /// <param name="tags">Optional tags for categorization.</param>
    /// <returns>A new Feedback instance with Explicit type.</returns>
    public static Feedback Explicit(
        string sourceId,
        string inputContext,
        string output,
        double score,
        params string[] tags)
        => new(
            Guid.NewGuid(),
            sourceId,
            inputContext,
            output,
            Math.Clamp(score, -1.0, 1.0),
            FeedbackType.Explicit,
            DateTime.UtcNow,
            tags.ToImmutableList());

    /// <summary>
    /// Creates implicit feedback inferred from user behavior.
    /// </summary>
    /// <param name="sourceId">Identifier of the producing component.</param>
    /// <param name="inputContext">The input context.</param>
    /// <param name="output">The produced output.</param>
    /// <param name="score">Inferred quality score in [-1, 1].</param>
    /// <param name="tags">Optional tags for categorization.</param>
    /// <returns>A new Feedback instance with Implicit type.</returns>
    public static Feedback Implicit(
        string sourceId,
        string inputContext,
        string output,
        double score,
        params string[] tags)
        => new(
            Guid.NewGuid(),
            sourceId,
            inputContext,
            output,
            Math.Clamp(score, -1.0, 1.0),
            FeedbackType.Implicit,
            DateTime.UtcNow,
            tags.ToImmutableList());

    /// <summary>
    /// Creates corrective feedback with the preferred output.
    /// </summary>
    /// <param name="sourceId">Identifier of the producing component.</param>
    /// <param name="inputContext">The input context.</param>
    /// <param name="actualOutput">The output that was produced.</param>
    /// <param name="preferredOutput">The preferred/correct output (stored in metadata).</param>
    /// <param name="tags">Optional tags for categorization.</param>
    /// <returns>A new Feedback instance with Corrective type and negative score.</returns>
    public static Feedback Corrective(
        string sourceId,
        string inputContext,
        string actualOutput,
        string preferredOutput,
        params string[] tags)
        => new Feedback(
            Guid.NewGuid(),
            sourceId,
            inputContext,
            actualOutput,
            -0.5, // Negative score indicates correction needed
            FeedbackType.Corrective,
            DateTime.UtcNow,
            tags.ToImmutableList().Add($"preferred:{preferredOutput}"));

    /// <summary>
    /// Creates comparative feedback ranking one output against another.
    /// </summary>
    /// <param name="sourceId">Identifier of the producing component.</param>
    /// <param name="inputContext">The input context.</param>
    /// <param name="chosenOutput">The output that was preferred.</param>
    /// <param name="rejectedOutput">The output that was rejected.</param>
    /// <param name="preferenceStrength">How strongly the chosen output was preferred (0, 1].</param>
    /// <param name="tags">Optional tags for categorization.</param>
    /// <returns>A new Feedback instance with Comparative type.</returns>
    public static Feedback Comparative(
        string sourceId,
        string inputContext,
        string chosenOutput,
        string rejectedOutput,
        double preferenceStrength = 0.5,
        params string[] tags)
        => new Feedback(
            Guid.NewGuid(),
            sourceId,
            inputContext,
            chosenOutput,
            Math.Clamp(preferenceStrength, 0.0, 1.0),
            FeedbackType.Comparative,
            DateTime.UtcNow,
            tags.ToImmutableList().Add($"rejected:{rejectedOutput}"));

    /// <summary>
    /// Creates a copy with additional tags.
    /// </summary>
    /// <param name="newTags">Tags to add.</param>
    /// <returns>A new Feedback with the additional tags.</returns>
    public Feedback WithTags(params string[] newTags)
        => this with { Tags = Tags.AddRange(newTags) };

    /// <summary>
    /// Validates the feedback data.
    /// </summary>
    /// <returns>A Result indicating success or validation errors.</returns>
    public Result<Unit, string> Validate()
    {
        if (string.IsNullOrWhiteSpace(SourceId))
        {
            return Result<Unit, string>.Failure("SourceId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(InputContext))
        {
            return Result<Unit, string>.Failure("InputContext cannot be empty.");
        }

        if (Score < -1.0 || Score > 1.0)
        {
            return Result<Unit, string>.Failure($"Score must be in [-1, 1], got {Score}.");
        }

        return Result<Unit, string>.Success(Unit.Value);
    }
}