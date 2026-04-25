// <copyright file="TensorServiceOptions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.ComponentModel.DataAnnotations;

namespace Ouroboros.Tensor.Configuration;

/// <summary>
/// Configuration options for connecting to the remote tensor service (Docker PyTorch).
/// </summary>
/// <remarks>
/// <para>
/// Used with .NET options pattern: services.Configure&lt;TensorServiceOptions&gt;(config.GetSection("TensorService")).
/// </para>
/// <para>
/// The remote service provides GPU-accelerated tensor operations via HTTP API (MatMul, FFT, CosineSimilarity).
/// </para>
/// </remarks>
public sealed class TensorServiceOptions
{
    /// <summary>
    /// Gets the base URL of the tensor service.
    /// </summary>
    /// <remarks>
    /// Default: http://localhost:8768 (matches Docker tensor_service port).
    /// </remarks>
    [Required]
    public Uri BaseUrl { get; init; } = new Uri("http://localhost:8768");

    /// <summary>
    /// Gets the timeout in seconds for HTTP requests.
    /// </summary>
    /// <remarks>
    /// Default: 30 seconds. Large matrix multiplications may need longer.
    /// </remarks>
    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Gets the maximum number of retry attempts for failed requests.
    /// </summary>
    /// <remarks>
    /// Default: 3 attempts. Uses exponential backoff between retries.
    /// </remarks>
    [Range(0, 10)]
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>
    /// Gets the initial delay in milliseconds between retries.
    /// </summary>
    /// <remarks>
    /// Default: 100ms. Doubles with each retry attempt.
    /// </remarks>
    [Range(10, 10000)]
    public int RetryDelayMs { get; init; } = 100;

    /// <summary>
    /// Gets a value indicating whether gets whether GPU acceleration is preferred.
    /// </summary>
    /// <remarks>
    /// Default: true. If GPU unavailable, falls back to CPU in the remote service.
    /// </remarks>
    public bool EnableGpu { get; init; } = true;

    /// <summary>
    /// Validates the options and returns a result indicating success or failure.
    /// </summary>
    /// <returns>Success if valid, Failure with error message if invalid.</returns>
    public Result<TensorServiceOptions, string> Validate()
    {
        if (BaseUrl is null)
        {
            return Result<TensorServiceOptions, string>.Failure("BaseUrl is required.");
        }

        if (!BaseUrl.IsAbsoluteUri)
        {
            return Result<TensorServiceOptions, string>.Failure("BaseUrl must be an absolute URI.");
        }

        if (TimeoutSeconds < 1 || TimeoutSeconds > 300)
        {
            return Result<TensorServiceOptions, string>.Failure("TimeoutSeconds must be between 1 and 300.");
        }

        if (MaxRetryAttempts < 0 || MaxRetryAttempts > 10)
        {
            return Result<TensorServiceOptions, string>.Failure("MaxRetryAttempts must be between 0 and 10.");
        }

        if (RetryDelayMs < 10 || RetryDelayMs > 10000)
        {
            return Result<TensorServiceOptions, string>.Failure("RetryDelayMs must be between 10 and 10000.");
        }

        return Result<TensorServiceOptions, string>.Success(this);
    }
}
