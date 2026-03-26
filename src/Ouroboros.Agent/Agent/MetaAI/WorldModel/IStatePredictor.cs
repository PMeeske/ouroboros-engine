// <copyright file="IPredictors.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Tensor.Abstractions;

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

/// <summary>
/// Factory interface for creating state predictors with optional GPU acceleration.
/// Enables clean DI registration and tensor backend injection.
/// </summary>
/// <remarks>
/// <para>
/// When <c>backend</c> is provided, predictors use GPU-accelerated matrix operations
/// via <see cref="ITensorBackend.MatMul"/>. When null, they fall back to CPU implementation.
/// </para>
/// <para>
/// DI registration example:
/// <code>
/// services.AddSingleton&lt;IStatePredictorFactory, StatePredictorFactory&gt;();
/// services.AddRemoteTensorBackend(); // Optional GPU acceleration
/// </code>
/// </para>
/// </remarks>
public interface IStatePredictorFactory
{
    /// <summary>
    /// Creates an MLP-based state predictor.
    /// </summary>
    /// <param name="stateSize">Size of state embeddings.</param>
    /// <param name="actionSize">Size of action embeddings.</param>
    /// <param name="hiddenSize">Size of hidden layer.</param>
    /// <param name="backend">Optional tensor backend for GPU acceleration. When null, uses CPU implementation.</param>
    /// <param name="seed">Random seed for reproducibility.</param>
    /// <returns>A new MLP state predictor.</returns>
    IStatePredictor CreateMlpPredictor(
        int stateSize,
        int actionSize,
        int hiddenSize,
        ITensorBackend? backend = null,
        int seed = 42);

    /// <summary>
    /// Creates a GNN-based state predictor.
    /// </summary>
    /// <param name="stateSize">Size of state embeddings.</param>
    /// <param name="actionSize">Size of action embeddings.</param>
    /// <param name="hiddenSize">Size of hidden layer.</param>
    /// <param name="backend">Optional tensor backend for GPU acceleration. When null, uses CPU implementation.</param>
    /// <param name="seed">Random seed for reproducibility.</param>
    /// <returns>A new GNN state predictor.</returns>
    IStatePredictor CreateGnnPredictor(
        int stateSize,
        int actionSize,
        int hiddenSize,
        ITensorBackend? backend = null,
        int seed = 42);
}