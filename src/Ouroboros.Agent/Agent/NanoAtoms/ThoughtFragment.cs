// <copyright file="ThoughtFragment.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.NanoAtoms;

/// <summary>
/// The smallest unit of thought input for a NanoOuroborosAtom.
/// Represents a focused piece of reasoning that fits within a nano-context token budget.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="Content">The thought content text.</param>
/// <param name="Source">Origin of this fragment: "user", "goal-decomposer", "digest", "context".</param>
/// <param name="EstimatedTokens">Estimated token count for this fragment.</param>
/// <param name="GoalType">Inferred goal type from SubGoal routing.</param>
/// <param name="Complexity">Inferred complexity level.</param>
/// <param name="PreferredTier">Routing tier for model selection.</param>
/// <param name="Timestamp">When this fragment was created.</param>
/// <param name="Tags">Optional tags for categorization.</param>
public sealed record ThoughtFragment(
    Guid Id,
    string Content,
    string Source,
    int EstimatedTokens,
    SubGoalType GoalType,
    SubGoalComplexity Complexity,
    PathwayTier PreferredTier,
    DateTime Timestamp,
    string[] Tags)
{
    /// <summary>
    /// Creates a ThoughtFragment from a <see cref="SubGoal"/> (GoalDecomposer integration).
    /// </summary>
    /// <param name="subGoal">The sub-goal to convert.</param>
    /// <returns>A ThoughtFragment with routing metadata from the sub-goal.</returns>
    public static ThoughtFragment FromSubGoal(SubGoal subGoal)
    {
        ArgumentNullException.ThrowIfNull(subGoal);

        return new ThoughtFragment(
            Id: Guid.NewGuid(),
            Content: subGoal.Description,
            Source: "goal-decomposer",
            EstimatedTokens: EstimateTokenCount(subGoal.Description),
            GoalType: subGoal.Type,
            Complexity: subGoal.Complexity,
            PreferredTier: subGoal.PreferredTier,
            Timestamp: DateTime.UtcNow,
            Tags: [subGoal.Id]);
    }

    /// <summary>
    /// Creates a ThoughtFragment from raw text (naive chunking fallback).
    /// </summary>
    /// <param name="text">The text content.</param>
    /// <param name="index">Optional index for ordering.</param>
    /// <returns>A ThoughtFragment with inferred routing.</returns>
    public static ThoughtFragment FromText(string text, int index = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        SubGoal inferred = SubGoal.FromDescription(text, index);
        return new ThoughtFragment(
            Id: Guid.NewGuid(),
            Content: text,
            Source: "user",
            EstimatedTokens: EstimateTokenCount(text),
            GoalType: inferred.Type,
            Complexity: inferred.Complexity,
            PreferredTier: inferred.PreferredTier,
            Timestamp: DateTime.UtcNow,
            Tags: [$"chunk_{index}"]);
    }

    /// <summary>
    /// Estimates token count using the standard BPE approximation (1 token per ~4 characters).
    /// </summary>
    /// <param name="text">The text to estimate.</param>
    /// <returns>Estimated token count.</returns>
    public static int EstimateTokenCount(string text) =>
        string.IsNullOrEmpty(text) ? 0 : Math.Max(1, text.Length / 4);
}
