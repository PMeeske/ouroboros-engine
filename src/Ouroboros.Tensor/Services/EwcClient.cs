// <copyright file="EwcClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Ouroboros.Abstractions;
using Ouroboros.Abstractions.Monads;

namespace Ouroboros.Tensor.Services;

/// <summary>
/// HTTP client for the EWC (Elastic Weight Consolidation) endpoints on the Python tensor service.
/// Provides Fisher diagonal computation, anchor storage, penalty computation, and drift validation.
/// </summary>
/// <remarks>
/// All methods return <see cref="Result{TValue}"/> with string errors. If the Docker tensor service
/// is unavailable, methods return explicit failures (never silently continue).
/// Register via IHttpClientFactory with named client "EwcService".
/// </remarks>
public sealed class EwcClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _client;
    private readonly ILogger<EwcClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EwcClient"/> class.
    /// </summary>
    /// <param name="httpClient">Pre-configured HttpClient (from IHttpClientFactory named "EwcService").</param>
    /// <param name="logger">Logger instance.</param>
    public EwcClient(HttpClient httpClient, ILogger<EwcClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        _client = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Computes the Fisher Information diagonal via PyTorch autograd on the tensor service.
    /// </summary>
    /// <param name="weights">Flat 65536-element weight array (256x256 row-major).</param>
    /// <param name="trainingInputs">Batch of 256-dim input vectors.</param>
    /// <param name="trainingOutputs">Batch of 256-dim target vectors.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Fisher diagonal float[65536] normalized to [0,1], or failure.</returns>
    public async Task<Result<float[]>> ComputeFisherAsync(
        float[] weights,
        float[][] trainingInputs,
        float[][] trainingOutputs,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new { weights, training_inputs = trainingInputs, training_outputs = trainingOutputs };
            var response = await _client.PostAsJsonAsync("ewc/compute_fisher", payload, JsonOptions, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _logger.LogWarning("ComputeFisher failed: {StatusCode} {Body}", (int)response.StatusCode, body);
                return Result<float[]>.Failure($"ComputeFisher HTTP {(int)response.StatusCode}: {body}");
            }

            var result = await response.Content.ReadFromJsonAsync<FisherResponseDto>(JsonOptions, ct).ConfigureAwait(false);
            return Result<float[]>.Success(result!.FisherDiagonal);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "ComputeFisher timed out");
            return Result<float[]>.Failure($"ComputeFisher timed out: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "ComputeFisher HTTP error");
            return Result<float[]>.Failure($"ComputeFisher HTTP error: {ex.Message}");
        }
    }

    /// <summary>
    /// Stores anchor weights on the tensor service for EWC comparison.
    /// </summary>
    /// <param name="weights">Flat 65536-element weight snapshot.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or failure.</returns>
    public async Task<Result<Unit>> StoreAnchorAsync(float[] weights, CancellationToken ct = default)
    {
        try
        {
            var payload = new { weights };
            var response = await _client.PostAsJsonAsync("ewc/store_anchor", payload, JsonOptions, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _logger.LogWarning("StoreAnchor failed: {StatusCode} {Body}", (int)response.StatusCode, body);
                return Result<Unit>.Failure($"StoreAnchor HTTP {(int)response.StatusCode}: {body}");
            }

            return Result<Unit>.Success(Unit.Value);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "StoreAnchor timed out");
            return Result<Unit>.Failure($"StoreAnchor timed out: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "StoreAnchor HTTP error");
            return Result<Unit>.Failure($"StoreAnchor HTTP error: {ex.Message}");
        }
    }

    /// <summary>
    /// Computes the EWC penalty: lambda * sum(fisher * (current - anchor)^2).
    /// </summary>
    /// <param name="currentWeights">Flat 65536-element current weight array.</param>
    /// <param name="lambdaValue">EWC lambda (1000 identity, 100 emotional, 10 expression).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Penalty scalar and max drift, or failure.</returns>
    public async Task<Result<PenaltyResult>> ComputePenaltyAsync(
        float[] currentWeights,
        float lambdaValue,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new { current_weights = currentWeights, lambda_value = lambdaValue };
            var response = await _client.PostAsJsonAsync("ewc/compute_penalty", payload, JsonOptions, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _logger.LogWarning("ComputePenalty failed: {StatusCode} {Body}", (int)response.StatusCode, body);
                return Result<PenaltyResult>.Failure($"ComputePenalty HTTP {(int)response.StatusCode}: {body}");
            }

            var result = await response.Content.ReadFromJsonAsync<PenaltyResult>(JsonOptions, ct).ConfigureAwait(false);
            return Result<PenaltyResult>.Success(result!);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "ComputePenalty timed out");
            return Result<PenaltyResult>.Failure($"ComputePenalty timed out: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "ComputePenalty HTTP error");
            return Result<PenaltyResult>.Failure($"ComputePenalty HTTP error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the current EWC state from the tensor service (fisher/anchor initialization status).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>EWC status record, or failure.</returns>
    public async Task<Result<EwcStatus>> GetStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _client.GetAsync("ewc/status", ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _logger.LogWarning("GetStatus failed: {StatusCode} {Body}", (int)response.StatusCode, body);
                return Result<EwcStatus>.Failure($"GetStatus HTTP {(int)response.StatusCode}: {body}");
            }

            var result = await response.Content.ReadFromJsonAsync<EwcStatus>(JsonOptions, ct).ConfigureAwait(false);
            return Result<EwcStatus>.Success(result!);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "GetStatus timed out");
            return Result<EwcStatus>.Failure($"GetStatus timed out: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "GetStatus HTTP error");
            return Result<EwcStatus>.Failure($"GetStatus HTTP error: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates whether current weights have drifted beyond threshold from stored anchor.
    /// </summary>
    /// <param name="currentWeights">Flat 65536-element current weight array.</param>
    /// <param name="threshold">Maximum allowed drift.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Drift result with within_threshold, max_drift, mean_drift, or failure.</returns>
    public async Task<Result<DriftResult>> ValidateDriftAsync(
        float[] currentWeights,
        float threshold,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new { current_weights = currentWeights, threshold };
            var response = await _client.PostAsJsonAsync("ewc/validate_drift", payload, JsonOptions, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _logger.LogWarning("ValidateDrift failed: {StatusCode} {Body}", (int)response.StatusCode, body);
                return Result<DriftResult>.Failure($"ValidateDrift HTTP {(int)response.StatusCode}: {body}");
            }

            var result = await response.Content.ReadFromJsonAsync<DriftResult>(JsonOptions, ct).ConfigureAwait(false);
            return Result<DriftResult>.Success(result!);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "ValidateDrift timed out");
            return Result<DriftResult>.Failure($"ValidateDrift timed out: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "ValidateDrift HTTP error");
            return Result<DriftResult>.Failure($"ValidateDrift HTTP error: {ex.Message}");
        }
    }

    // ── Internal DTOs ────────────────────────────────────────────────────────

    private sealed record FisherResponseDto(float[] FisherDiagonal, int SampleCount);
}

/// <summary>
/// EWC status from the tensor service.
/// </summary>
/// <param name="FisherInitialized">Whether Fisher diagonal has been computed.</param>
/// <param name="AnchorInitialized">Whether anchor weights have been stored.</param>
/// <param name="FisherShape">Shape of the Fisher tensor (e.g. [256, 256]), or null.</param>
/// <param name="Device">Device string (e.g. "cuda" or "cpu").</param>
public sealed record EwcStatus(bool FisherInitialized, bool AnchorInitialized, int[]? FisherShape, string Device);

/// <summary>
/// Result of drift validation against stored anchor.
/// </summary>
/// <param name="WithinThreshold">Whether max drift is within allowed threshold.</param>
/// <param name="MaxDrift">Maximum absolute weight change from anchor.</param>
/// <param name="MeanDrift">Mean absolute weight change from anchor.</param>
public sealed record DriftResult(bool WithinThreshold, float MaxDrift, float MeanDrift);

/// <summary>
/// Result of EWC penalty computation.
/// </summary>
/// <param name="Penalty">EWC penalty scalar: lambda * sum(fisher * (current - anchor)^2).</param>
/// <param name="MaxDrift">Maximum absolute weight change from anchor.</param>
public sealed record PenaltyResult(float Penalty, float MaxDrift);
