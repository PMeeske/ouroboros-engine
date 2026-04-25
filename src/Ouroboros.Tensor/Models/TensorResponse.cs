// <copyright file="TensorResponse.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;

namespace Ouroboros.Tensor.Models;

/// <summary>
/// Response containing tensor data (shape + flattened values).
/// </summary>
/// <param name="Shape">The output tensor dimensions.</param>
/// <param name="Data">The flattened tensor data in row-major order.</param>
public sealed record TensorDataResponse(
    [property: JsonPropertyName("shape")] List<int> Shape,
    [property: JsonPropertyName("data")] List<float> Data);

/// <summary>
/// Response for matrix multiplication.
/// </summary>
/// <param name="Result">The resulting matrix from A × B.</param>
public sealed record MatMulResponse(
    [property: JsonPropertyName("result")] TensorDataResponse Result);

/// <summary>
/// Response for FFT operation.
/// </summary>
/// <param name="Result">The FFT output (complex values as float pairs).</param>
public sealed record FftResponse(
    [property: JsonPropertyName("result")] TensorDataResponse Result);

/// <summary>
/// Response for cosine similarity computation.
/// </summary>
/// <param name="Similarity">The cosine similarity value between two vectors.</param>
public sealed record CosineSimilarityResponse(
    [property: JsonPropertyName("similarity")] double Similarity);

/// <summary>
/// Response for health check endpoint.
/// </summary>
/// <param name="Status">Service health status ("healthy" or "unhealthy").</param>
/// <param name="Device">Compute device ("cuda" or "cpu").</param>
public sealed record HealthResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("device")] string Device);
