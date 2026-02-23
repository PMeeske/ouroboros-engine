// <copyright file="HybridStatePredictor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Hybrid state predictor that combines Transformer temporal attention with GNN
/// relational reasoning. Runs both predictors in parallel and averages their
/// embedding outputs to capture complementary aspects of state dynamics.
/// </summary>
public sealed class HybridStatePredictor : IStatePredictor
{
    private readonly TransformerStatePredictor _transformer;
    private readonly GnnStatePredictor _gnn;

    /// <summary>
    /// Initializes a new instance of the <see cref="HybridStatePredictor"/> class.
    /// </summary>
    /// <param name="transformer">The Transformer-based predictor for temporal attention.</param>
    /// <param name="gnn">The GNN-based predictor for relational reasoning.</param>
    public HybridStatePredictor(TransformerStatePredictor transformer, GnnStatePredictor gnn)
    {
        _transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));
        _gnn = gnn ?? throw new ArgumentNullException(nameof(gnn));
    }

    /// <inheritdoc/>
    public async Task<State> PredictAsync(State current, Action action, CancellationToken ct = default)
    {
        // Run both predictors concurrently
        Task<State> transformerTask = _transformer.PredictAsync(current, action, ct);
        Task<State> gnnTask = _gnn.PredictAsync(current, action, ct);

        await Task.WhenAll(transformerTask, gnnTask);

        State transformerState = transformerTask.Result;
        State gnnState = gnnTask.Result;

        // Average embeddings from both predictors
        int length = Math.Min(transformerState.Embedding.Length, gnnState.Embedding.Length);
        float[] combinedEmbedding = new float[length];
        for (int i = 0; i < length; i++)
        {
            combinedEmbedding[i] = (transformerState.Embedding[i] + gnnState.Embedding[i]) * 0.5f;
        }

        return new State(
            Features: new Dictionary<string, object>(current.Features),
            Embedding: combinedEmbedding);
    }
}
