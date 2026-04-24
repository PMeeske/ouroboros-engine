// <copyright file="StubGazeEstimator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Tensor.Abstractions;

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Phase 240 stub <see cref="IGazeEstimator"/>: returns zero yaw/pitch with a
/// fixed low confidence so downstream consumers can verify OOD tagging.
/// </summary>
public sealed class StubGazeEstimator : IGazeEstimator
{
    private const float StubConfidence = 0.2f;

    private readonly ILogger<StubGazeEstimator>? _logger;

    /// <summary>Initializes a new instance of the <see cref="StubGazeEstimator"/> class.</summary>
    /// <param name="logger">Optional logger.</param>
    public StubGazeEstimator(ILogger<StubGazeEstimator>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<GazeEstimate?> EstimateAsync(FrameBuffer face, CancellationToken cancellationToken)
    {
        if (face is null || face.Width <= 0 || face.Height <= 0)
        {
            return Task.FromResult<GazeEstimate?>(null);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<GazeEstimate?>(
                new GazeEstimate(0f, 0f, StubConfidence));
        }
        catch (OperationCanceledException) { throw; }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogDebug(ex, "[StubGazeEstimator] stub failed");
            return Task.FromResult<GazeEstimate?>(null);
        }
    }
}
