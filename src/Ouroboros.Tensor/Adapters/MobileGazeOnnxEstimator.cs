// <copyright file="MobileGazeOnnxEstimator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Abstractions.Monads;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Orchestration;

namespace Ouroboros.Tensor.Adapters;

/// <summary>Phase 240 MobileGaze ONNX estimator scaffold with VRAM gate.</summary>
public sealed class MobileGazeOnnxEstimator : IGazeEstimator, IAsyncDisposable
{
    /// <summary>MobileGaze VRAM estimate (~12 MiB FP32).</summary>
    public const long EstimatedBytes = 14L * 1024L * 1024L;

    /// <summary>Stable model identifier for <see cref="IPerceptionVramBudget"/>.</summary>
    public const string ModelId = "mobilegaze";

    private readonly string _modelPath;
    private readonly IPerceptionVramBudget _budget;
    private readonly GpuScheduler _scheduler;
    private readonly StubGazeEstimator _fallback;
    private readonly ILogger<MobileGazeOnnxEstimator>? _logger;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IVramReservation? _reservation;
    private int _state;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="MobileGazeOnnxEstimator"/> class.</summary>
    /// <param name="modelPath">Absolute path to the MobileGaze ONNX file.</param>
    /// <param name="budget">Perception VRAM admission gate.</param>
    /// <param name="scheduler">Shared GPU scheduler.</param>
    /// <param name="logger">Optional logger.</param>
    public MobileGazeOnnxEstimator(
        string modelPath,
        IPerceptionVramBudget budget,
        GpuScheduler scheduler,
        ILogger<MobileGazeOnnxEstimator>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(scheduler);
        _modelPath = modelPath;
        _budget = budget;
        _scheduler = scheduler;
        _fallback = new StubGazeEstimator(null);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<GazeEstimate?> EstimateAsync(FrameBuffer face, CancellationToken cancellationToken)
    {
        if (_disposed || face is null) return null;

        cancellationToken.ThrowIfCancellationRequested();

        if (_state == 0)
        {
            await EnsureInitialisedAsync(cancellationToken).ConfigureAwait(false);
        }

        _ = _scheduler;
        return await _fallback.EstimateAsync(face, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        IVramReservation? reservation = Interlocked.Exchange(ref _reservation, null);
        if (reservation is not null)
        {
            try { await reservation.DisposeAsync().ConfigureAwait(false); }
#pragma warning disable CA1031
            catch (Exception ex) { _logger?.LogDebug(ex, "[MobileGazeOnnx] reservation dispose failed"); }
#pragma warning restore CA1031
        }

        _initLock.Dispose();
    }

    private async Task EnsureInitialisedAsync(CancellationToken ct)
    {
        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_state != 0) return;

            if (!File.Exists(_modelPath))
            {
                _logger?.LogInformation(
                    "[MobileGazeOnnx] model '{Path}' not found; gaze estimation degrades to stub",
                    _modelPath);
                Interlocked.Exchange(ref _state, -1);
                return;
            }

            Result<IVramReservation> reservation = await _budget
                .TryReserveAsync(ModelId, EstimatedBytes, ct)
                .ConfigureAwait(false);

            if (!reservation.IsSuccess)
            {
                _logger?.LogWarning(
                    "[MobileGazeOnnx] VRAM reservation denied: {Err}; degrading to stub",
                    reservation.Error);
                Interlocked.Exchange(ref _state, -1);
                return;
            }

            _reservation = reservation.Value;
            _logger?.LogInformation(
                "[MobileGazeOnnx] VRAM reservation acquired ({Bytes:N0} bytes) — ONNX session deferred",
                EstimatedBytes);
            Interlocked.Exchange(ref _state, 1);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogWarning(ex, "[MobileGazeOnnx] init failed; degrading to stub");
            Interlocked.Exchange(ref _state, -1);
        }
        finally
        {
            _initLock.Release();
        }
    }
}
