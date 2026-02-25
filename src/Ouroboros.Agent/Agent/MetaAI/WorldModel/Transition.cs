// <copyright file="WorldModelTypes.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Represents a transition in an environment - a complete experience tuple.
/// Immutable record following functional programming principles.
/// </summary>
/// <param name="PreviousState">The state before the action was taken.</param>
/// <param name="ActionTaken">The action that was executed.</param>
/// <param name="NextState">The resulting state after the action.</param>
/// <param name="Reward">The reward received for this transition.</param>
/// <param name="Terminal">Whether the next state is terminal.</param>
public sealed record Transition(
    State PreviousState,
    Action ActionTaken,
    State NextState,
    double Reward,
    bool Terminal);