// <copyright file="TensorServiceClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ouroboros.Tensor.Configuration;

namespace Ouroboros.Tensor.Models;

/// <summary>
/// HTTP client wrapper for the remote tensor service (Docker PyTorch).
/// Provides typed methods for MatMul, FFT, CosineSimilarity, and health checks.
/// </summary>
/// <remarks>
/// <para>
/// Uses IHttpClientFactory pattern for testability and connection pooling.
/// Implements exponential backoff retry for transient failures.
/// </para>
/// <para>
/// All methods are async and support cancellation tokens.
/// </para>
/// </remarks>
public sealed class TensorServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly TensorServiceOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="TensorServiceClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with base address.</param>
    /// <param name="options">The tensor service configuration options.</param>
    public TensorServiceClient(HttpClient httpClient, TensorServiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Gets the health status and device type from the tensor service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health response with status and device type.</returns>
    /// <exception cref="HttpRequestException">Thrown when the request fails after all retries.</exception>
    public async Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<HealthResponse>(_jsonOptions, cancellationToken)
                .ConfigureAwait(false) ?? throw new InvalidOperationException("Health response was null.");
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs matrix multiplication on the remote tensor service.
    /// </summary>
    /// <param name="a">First input tensor (matrix A).</param>
    /// <param name="b">Second input tensor (matrix B).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result tensor from A × B.</returns>
    /// <exception cref="HttpRequestException">Thrown when the request fails after all retries.</exception>
    public async Task<TensorDataResponse> MatMulAsync(
        TensorData a,
        TensorData b,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var request = new MatMulRequest(a, b);
        return await ExecuteWithRetryAsync(async () =>
        {
            var response = await _httpClient.PostAsJsonAsync("/tensor/matmul", request, _jsonOptions, cancellationToken)
                .ConfigureAwait(false);
            await EnsureSuccessStatusCodeAsync(response, cancellationToken).ConfigureAwait(false);
            var result = await response.Content.ReadFromJsonAsync<MatMulResponse>(_jsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return result?.Result ?? throw new InvalidOperationException("MatMul response was null.");
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs Fast Fourier Transform on the remote tensor service.
    /// </summary>
    /// <param name="input">Input tensor for FFT.</param>
    /// <param name="dimensions">Number of dimensions for FFT (default: 1).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>FFT result tensor (complex values as float pairs).</returns>
    /// <exception cref="HttpRequestException">Thrown when the request fails after all retries.</exception>
    public async Task<TensorDataResponse> FftAsync(
        TensorData input,
        int dimensions = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var request = new FftRequest(input);
        return await ExecuteWithRetryAsync(async () =>
        {
            var url = $"/tensor/fft?dimensions={dimensions}";
            var response = await _httpClient.PostAsJsonAsync(url, request, _jsonOptions, cancellationToken)
                .ConfigureAwait(false);
            await EnsureSuccessStatusCodeAsync(response, cancellationToken).ConfigureAwait(false);
            var result = await response.Content.ReadFromJsonAsync<FftResponse>(_jsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return result?.Result ?? throw new InvalidOperationException("FFT response was null.");
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Computes cosine similarity between two vectors on the remote tensor service.
    /// </summary>
    /// <param name="a">First input vector.</param>
    /// <param name="b">Second input vector.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cosine similarity value.</returns>
    /// <exception cref="HttpRequestException">Thrown when the request fails after all retries.</exception>
    public async Task<double> CosineSimilarityAsync(
        TensorData a,
        TensorData b,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var request = new CosineSimilarityRequest(a, b);
        return await ExecuteWithRetryAsync(async () =>
        {
            var response = await _httpClient.PostAsJsonAsync("/tensor/cosine_similarity", request, _jsonOptions, cancellationToken)
                .ConfigureAwait(false);
            await EnsureSuccessStatusCodeAsync(response, cancellationToken).ConfigureAwait(false);
            var result = await response.Content.ReadFromJsonAsync<CosineSimilarityResponse>(_jsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return result?.Similarity ?? throw new InvalidOperationException("CosineSimilarity response was null.");
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt <= _options.MaxRetryAttempts; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (IsTransient(ex))
            {
                lastException = ex;
                if (attempt < _options.MaxRetryAttempts)
                {
                    var delay = TimeSpan.FromMilliseconds(_options.RetryDelayMs * Math.Pow(2, attempt));
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                lastException = ex;
                if (attempt < _options.MaxRetryAttempts)
                {
                    var delay = TimeSpan.FromMilliseconds(_options.RetryDelayMs * Math.Pow(2, attempt));
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        throw lastException ?? new InvalidOperationException("Retry exhausted without exception.");
    }

    private static bool IsTransient(HttpRequestException exception)
    {
        // Retry on 5xx server errors and network-related failures
        return exception.InnerException is TimeoutException
            || exception.Message.Contains("5")
            || exception.Message.Contains("connection", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException(
                $"Request failed with status {(int)response.StatusCode} {response.StatusCode}: {content}");
        }
    }
}