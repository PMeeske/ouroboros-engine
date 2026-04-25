// <copyright file="ITensorServiceClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Tensor.Configuration;

namespace Ouroboros.Tensor.Models;

/// <summary>
/// Interface for the remote tensor service HTTP client.
/// Enables mocking and unit testing of remote backends (R15).
/// </summary>
public interface ITensorServiceClient
{
    /// <summary>
    /// Gets the health status and device type from the tensor service.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs matrix multiplication on the remote tensor service.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    Task<TensorDataResponse> MatMulAsync(TensorData a, TensorData b, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs Fast Fourier Transform on the remote tensor service.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    Task<TensorDataResponse> FftAsync(TensorData input, int dimensions = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes cosine similarity between two vectors on the remote tensor service.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    Task<double> CosineSimilarityAsync(TensorData a, TensorData b, CancellationToken cancellationToken = default);
}
