// <copyright file="MoveNetOnnxPoseEstimator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Abstractions.Monads;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Orchestration;

namespace Ouroboros.Tensor.Adapters;

/// <summary>Phase 240 MoveNet ONNX pose-estimator scaffold with VRAM gate.</summary>
public sealed class MoveNetOnnxPoseEstimator : IPoseEstimator, IAsyncDisposable
{
    /// <summary>Conservative VRAM estimate for MoveNet Thunder FP16 (~25 MiB).</summary>
    public const long EstimatedBytes = 28L * 1024L * 1024L;

    /// <summary>Stable model identifier for <see cref="IPerceptionVramBudget"/>.</summary>
    public const string ModelId = "movenet";

    private readonly string _modelPath;
    private readonly IPerceptionVramBudget _budget;
    private readonly GpuScheduler _scheduler;
    private readonly StubPoseEstimator _fallback;
    private readonly ILogger<MoveNetOnnxPoseEstimator>? _logger;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IVramReservation? _reservation;
    private int _state;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="MoveNetOnnxPoseEstimator"/> class.</summary>
    /// <param name="modelPath">Absolute path to the MoveNet ONNX file.</param>
    /// <param name="budget">Perception VRAM admission gate.</param>
    /// <param name="scheduler">Shared GPU scheduler.</param>
    /// <param name="logger">Optional logger.</param>
    public MoveNetOnnxPoseEstimator(
        string modelPath,
        IPerceptionVramBudget budget,
        GpuScheduler scheduler,
        ILogger<MoveNetOnnxPoseEstimator>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(scheduler);
        _modelPath = modelPath;
        _budget = budget;
        _scheduler = scheduler;
        _fallback = new StubPoseEstimator(null);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PoseEstimate?> EstimateAsync(FrameBuffer frame, CancellationToken cancellationToken)
    {
        if (_disposed || frame is null) return null;

        cancellationToken.ThrowIfCancellationRequested();

        if (_state == 0)
        {
            await EnsureInitialisedAsync(cancellationToken).ConfigureAwait(false);
        }

        _ = _scheduler;
        return await _fallback.EstimateAsync(frame, cancellationToken).ConfigureAwait(false);
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
            catch (Exception ex) { _logger?.LogDebug(ex, "[MoveNetOnnx] reservation dispose failed"); }
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
                    "[MoveNetOnnx] model '{Path}' not found; pose estimation degrades to stub",
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
                    "[MoveNetOnnx] VRAM reservation denied: {Err}; degrading to stub",
                    reservation.Error);
                Interlocked.Exchange(ref _state, -1);
                return;
            }

            _reservation = reservation.Value;
            _logger?.LogInformation(
                "[MoveNetOnnx] VRAM reservation acquired ({Bytes:N0} bytes) — ONNX session deferred",
                EstimatedBytes);
            Interlocked.Exchange(ref _state, 1);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogWarning(ex, "[MoveNetOnnx] init failed; degrading to stub");
            Interlocked.Exchange(ref _state, -1);
        }
        finally
        {
            _initLock.Release();
        }
    }
}
