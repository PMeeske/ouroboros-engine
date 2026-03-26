// <copyright file="TensorRequest.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;

namespace Ouroboros.Tensor.Models;

/// <summary>
/// Represents tensor data for HTTP transport (shape + flattened data).
/// </summary>
/// <param name="Shape">The tensor dimensions (e.g., [2, 3] for a 2x3 matrix).</param>
/// <param name="Data">The flattened tensor data in row-major order.</param>
public sealed record TensorData(
    [property: JsonPropertyName("shape")] List<int> Shape,
    [property: JsonPropertyName("data")] List<float> Data);

/// <summary>
/// Request payload for matrix multiplication.
/// </summary>
/// <param name="A">First input tensor (matrix A).</param>
/// <param name="B">Second input tensor (matrix B).</param>
public sealed record MatMulRequest(
    [property: JsonPropertyName("a")] TensorData A,
    [property: JsonPropertyName("b")] TensorData B);

/// <summary>
/// Request payload for FFT operation.
/// </summary>
/// <param name="Input">Input tensor for FFT computation.</param>
public sealed record FftRequest(
    [property: JsonPropertyName("input")] TensorData Input);

/// <summary>
/// Request payload for cosine similarity computation.
/// </summary>
/// <param name="A">First input vector.</param>
/// <param name="B">Second input vector.</param>
public sealed record CosineSimilarityRequest(
    [property: JsonPropertyName("a")] TensorData A,
    [property: JsonPropertyName("b")] TensorData B);