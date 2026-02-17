// <copyright file="IPredictors.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Interface for predicting next states given current state and action.
/// Follows functional programming principles with async operations.
/// </summary>
public interface IStatePredictor
{
    /// <summary>
    /// Predicts the next state given current state and action.
    /// </summary>
    /// <param name="current">The current state.</param>
    /// <param name="action">The action to take.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The predicted next state.</returns>
    Task<State> PredictAsync(State current, Action action, CancellationToken ct = default);
}