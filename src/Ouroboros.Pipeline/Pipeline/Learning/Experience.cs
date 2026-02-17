// <copyright file="ExperienceReplay.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Learning;

using System.Collections.Immutable;
using Ouroboros.Core.Monads;

/// <summary>
/// Represents a single experience for replay-based learning.
/// Captures the state-action-reward-next_state tuple essential for reinforcement learning.
/// </summary>
/// <param name="Id">Unique identifier for this experience.</param>
/// <param name="State">The input state or context before the action.</param>
/// <param name="Action">The action taken in response to the state.</param>
/// <param name="Reward">The feedback score received for the action (typically in range [-1, 1] or [0, 1]).</param>
/// <param name="NextState">The resulting state after the action was taken.</param>
/// <param name="Timestamp">When this experience was recorded.</param>
/// <param name="Metadata">Additional contextual information about the experience.</param>
/// <param name="Priority">Priority weight for prioritized replay sampling (higher = more likely to be sampled).</param>
public sealed record Experience(
    Guid Id,
    string State,
    string Action,
    double Reward,
    string NextState,
    DateTime Timestamp,
    ImmutableDictionary<string, object> Metadata,
    double Priority)
{
    /// <summary>
    /// Creates a new experience with auto-generated ID and current timestamp.
    /// </summary>
    /// <param name="state">The input state or context.</param>
    /// <param name="action">The action taken.</param>
    /// <param name="reward">The feedback score.</param>
    /// <param name="nextState">The resulting state.</param>
    /// <param name="priority">Priority for replay sampling (default: 1.0).</param>
    /// <param name="metadata">Optional metadata dictionary.</param>
    /// <returns>A new Experience instance.</returns>
    public static Experience Create(
        string state,
        string action,
        double reward,
        string nextState,
        double priority = 1.0,
        ImmutableDictionary<string, object>? metadata = null)
        => new(
            Guid.NewGuid(),
            state,
            action,
            reward,
            nextState,
            DateTime.UtcNow,
            metadata ?? ImmutableDictionary<string, object>.Empty,
            priority);

    /// <summary>
    /// Creates a new experience with adjusted priority based on TD-error.
    /// </summary>
    /// <param name="tdError">The temporal difference error magnitude.</param>
    /// <param name="epsilon">Small constant to ensure non-zero priority (default: 0.01).</param>
    /// <returns>A new Experience with updated priority.</returns>
    public Experience WithTDErrorPriority(double tdError, double epsilon = 0.01)
        => this with { Priority = Math.Abs(tdError) + epsilon };

    /// <summary>
    /// Creates a copy with updated metadata.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>A new Experience with the added metadata.</returns>
    public Experience WithMetadata(string key, object value)
        => this with { Metadata = Metadata.SetItem(key, value) };
}