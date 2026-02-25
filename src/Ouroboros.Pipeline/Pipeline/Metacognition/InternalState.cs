// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Introspection.cs" company="Ouroboros">
//   Copyright (c) Ouroboros. All rights reserved.
//   Licensed under the MIT License.
// </copyright>
// <summary>
//   Implements introspection capabilities for self-reflection and metacognition.
//   Provides mechanisms to examine and reason about internal cognitive states.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Ouroboros.Pipeline.Metacognition;

using System;
using System.Collections.Immutable;
using Ouroboros.Core.Monads;

/// <summary>
/// Represents a snapshot of the agent's internal cognitive state at a specific moment.
/// This immutable record captures all relevant aspects of cognitive functioning.
/// </summary>
public sealed record InternalState(
    Guid Id,
    DateTime Timestamp,
    ImmutableList<string> ActiveGoals,
    string CurrentFocus,
    double CognitiveLoad,
    double EmotionalValence,
    ImmutableDictionary<string, double> AttentionDistribution,
    ImmutableList<string> WorkingMemoryItems,
    ProcessingMode Mode)
{
    /// <summary>
    /// Creates an initial state with default values.
    /// </summary>
    /// <returns>A new InternalState with neutral defaults.</returns>
    public static InternalState Initial() => new(
        Guid.NewGuid(),
        DateTime.UtcNow,
        ImmutableList<string>.Empty,
        "None",
        0.0,
        0.0,
        ImmutableDictionary<string, double>.Empty,
        ImmutableList<string>.Empty,
        ProcessingMode.Reactive);

    /// <summary>
    /// Creates a copy with updated timestamp and new ID.
    /// </summary>
    /// <returns>A fresh snapshot based on current state.</returns>
    public InternalState Snapshot() => this with
    {
        Id = Guid.NewGuid(),
        Timestamp = DateTime.UtcNow
    };

    /// <summary>
    /// Adds a goal to the active goals list.
    /// </summary>
    /// <param name="goal">The goal to add.</param>
    /// <returns>New state with the goal added.</returns>
    public InternalState WithGoal(string goal) =>
        string.IsNullOrWhiteSpace(goal)
            ? this
            : this with { ActiveGoals = ActiveGoals.Add(goal) };

    /// <summary>
    /// Removes a goal from the active goals list.
    /// </summary>
    /// <param name="goal">The goal to remove.</param>
    /// <returns>New state with the goal removed.</returns>
    public InternalState WithoutGoal(string goal) =>
        this with { ActiveGoals = ActiveGoals.Remove(goal) };

    /// <summary>
    /// Updates the current focus.
    /// </summary>
    /// <param name="focus">The new focus area.</param>
    /// <returns>New state with updated focus.</returns>
    public InternalState WithFocus(string focus) =>
        this with { CurrentFocus = focus ?? "None" };

    /// <summary>
    /// Updates the cognitive load value.
    /// </summary>
    /// <param name="load">The new load value (clamped to 0-1).</param>
    /// <returns>New state with updated cognitive load.</returns>
    public InternalState WithCognitiveLoad(double load) =>
        this with { CognitiveLoad = Math.Clamp(load, 0.0, 1.0) };

    /// <summary>
    /// Updates the emotional valence.
    /// </summary>
    /// <param name="valence">The new valence value (clamped to -1 to 1).</param>
    /// <returns>New state with updated valence.</returns>
    public InternalState WithValence(double valence) =>
        this with { EmotionalValence = Math.Clamp(valence, -1.0, 1.0) };

    /// <summary>
    /// Adds an item to working memory.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <returns>New state with the item in working memory.</returns>
    public InternalState WithWorkingMemoryItem(string item) =>
        string.IsNullOrWhiteSpace(item)
            ? this
            : this with { WorkingMemoryItems = WorkingMemoryItems.Add(item) };

    /// <summary>
    /// Sets attention distribution.
    /// </summary>
    /// <param name="distribution">The attention distribution map.</param>
    /// <returns>New state with updated attention.</returns>
    public InternalState WithAttention(ImmutableDictionary<string, double> distribution) =>
        this with { AttentionDistribution = distribution };

    /// <summary>
    /// Changes the processing mode.
    /// </summary>
    /// <param name="mode">The new processing mode.</param>
    /// <returns>New state with updated mode.</returns>
    public InternalState WithMode(ProcessingMode mode) =>
        this with { Mode = mode };
}