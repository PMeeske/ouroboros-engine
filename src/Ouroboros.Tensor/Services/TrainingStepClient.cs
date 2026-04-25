// <copyright file="TrainingStepClient.cs" company="Ouroboros">
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
/// HTTP client for the training loss and gradient step endpoints on the Python tensor service.
/// Provides GPU-accelerated L1+SSIM loss computation and SGD weight updates.
/// </summary>
/// <remarks>
/// Register via IHttpClientFactory with named client "TrainingService" pointing to tensor service base URL.
/// All methods return <see cref="Result{TValue}"/> with string errors. If the Docker tensor service
/// is unavailable, methods return explicit failures (never silently continue).
/// </remarks>
public sealed class TrainingStepClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _client;
    private readonly ILogger<TrainingStepClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrainingStepClient"/> class.
    /// </summary>
    /// <param name="httpClient">Pre-configured HttpClient (from IHttpClientFactory named "TrainingService").</param>
    /// <param name="logger">Logger instance.</param>
    public TrainingStepClient(HttpClient httpClient, ILogger<TrainingStepClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
        _client = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Computes L1 + SSIM loss between predicted and ground-truth images on the GPU tensor service.
    /// </summary>
    /// <param name="predictedPixels">Predicted RGB pixels as float[H*W*3] normalized to [0,1].</param>
    /// <param name="groundTruthPixels">Ground-truth RGB pixels as float[H*W*3] normalized to [0,1].</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="faceMask">Optional face mask float[H*W] in [0,1] for weighted loss computation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Loss result with L1, SSIM, and total loss values, or failure.</returns>
    public async Task<Result<LossResult>> ComputeLossAsync(
        float[] predictedPixels,
        float[] groundTruthPixels,
        int width,
        int height,
        float[]? faceMask = null,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                predicted = predictedPixels,
                ground_truth = groundTruthPixels,
                width,
                height,
                face_mask = faceMask,
            };

            var response = await _client.PostAsJsonAsync("training/loss", payload, JsonOptions, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _logger.LogWarning("ComputeLoss failed: {StatusCode} {Body}", (int)response.StatusCode, body);
                return Result<LossResult>.Failure($"ComputeLoss HTTP {(int)response.StatusCode}: {body}");
            }

            var result = await response.Content.ReadFromJsonAsync<LossResult>(JsonOptions, ct).ConfigureAwait(false);
            return Result<LossResult>.Success(result!);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "ComputeLoss timed out");
            return Result<LossResult>.Failure($"ComputeLoss timed out: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "ComputeLoss HTTP error");
            return Result<LossResult>.Failure($"ComputeLoss HTTP error: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies a single SGD gradient step on the GPU tensor service.
    /// Computes: updated = loraWeights - learningRate * gradient.
    /// </summary>
    /// <param name="loraWeights">Flat LoRA weight array.</param>
    /// <param name="gradient">Pre-computed gradient (same shape as weights).</param>
    /// <param name="learningRate">SGD learning rate (default 0.001).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated weight array, or failure.</returns>
    public async Task<Result<float[]>> ApplyGradientStepAsync(
        float[] loraWeights,
        float[] gradient,
        float learningRate = 0.001f,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                lora_weights = loraWeights,
                gradient,
                learning_rate = learningRate,
            };

            var response = await _client.PostAsJsonAsync("training/step", payload, JsonOptions, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _logger.LogWarning("ApplyGradientStep failed: {StatusCode} {Body}", (int)response.StatusCode, body);
                return Result<float[]>.Failure($"ApplyGradientStep HTTP {(int)response.StatusCode}: {body}");
            }

            var result = await response.Content.ReadFromJsonAsync<StepResponseDto>(JsonOptions, ct).ConfigureAwait(false);
            return Result<float[]>.Success(result!.UpdatedWeights);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "ApplyGradientStep timed out");
            return Result<float[]>.Failure($"ApplyGradientStep timed out: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "ApplyGradientStep HTTP error");
            return Result<float[]>.Failure($"ApplyGradientStep HTTP error: {ex.Message}");
        }
    }

    // ── Internal DTOs ────────────────────────────────────────────────────────
    private sealed record StepResponseDto(float[] UpdatedWeights);
}

/// <summary>
/// Result of GPU-computed image loss between predicted and ground-truth frames.
/// </summary>
/// <param name="L1Loss">Mean absolute difference in [0, 1].</param>
/// <param name="SsimLoss">Structural similarity index in [0, 1] (1 = identical).</param>
/// <param name="TotalLoss">Weighted combination: 0.8 * L1 + 0.2 * (1 - SSIM).</param>
public sealed record LossResult(float L1Loss, float SsimLoss, float TotalLoss);
