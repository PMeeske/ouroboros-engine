// <copyright file="SFaceOnnxEmbedder.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Abstractions.Monads;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Orchestration;

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Phase 239 SFace ONNX face embedder scaffold. Registers VRAM via
/// <see cref="IPerceptionVramBudget"/> before session creation and falls back
/// to stub embedding behaviour when the model file is missing or admission
/// is denied. Same "plumb first, validate later" pattern as
/// <see cref="YuNetOnnxFaceDetector"/>.
/// </summary>
public sealed class SFaceOnnxEmbedder : IFaceEmbedder, IAsyncDisposable
{
    /// <summary>
    /// Best-effort VRAM estimate for SFace (~30 MiB FP32). Conservative upper
    /// bound; source: OpenCV Zoo README.
    /// </summary>
    public const long EstimatedBytes = 32L * 1024L * 1024L;

    /// <summary>Stable model identifier for <see cref="IPerceptionVramBudget"/>.</summary>
    public const string ModelId = "sface";

    private readonly string _modelPath;
    private readonly IPerceptionVramBudget _budget;
    private readonly GpuScheduler _scheduler;
    private readonly StubFaceEmbedder _fallback;
    private readonly ILogger<SFaceOnnxEmbedder>? _logger;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IVramReservation? _reservation;
    private int _state; // 0 = uninitialised, 1 = ready, -1 = fallback
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="SFaceOnnxEmbedder"/> class.</summary>
    /// <param name="modelPath">Absolute path to <c>sface_recognition.onnx</c>.</param>
    /// <param name="budget">Perception VRAM admission gate.</param>
    /// <param name="scheduler">Shared GPU scheduler (used by future ONNX dispatch).</param>
    /// <param name="logger">Optional logger.</param>
    public SFaceOnnxEmbedder(
        string modelPath,
        IPerceptionVramBudget budget,
        GpuScheduler scheduler,
        ILogger<SFaceOnnxEmbedder>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(scheduler);
        _modelPath = modelPath;
        _budget = budget;
        _scheduler = scheduler;
        _fallback = new StubFaceEmbedder(null);
        _logger = logger;
    }

    /// <inheritdoc/>
    public int Dimensions => StubFaceEmbedder.EmbeddingDimensions;

    /// <inheritdoc/>
    public async Task<float[]?> EmbedAsync(FrameBuffer face, CancellationToken cancellationToken)
    {
        if (_disposed || face is null || face.Rgba is null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (_state == 0)
        {
            await EnsureInitialisedAsync(cancellationToken).ConfigureAwait(false);
        }

        // Until the decoder swap ships, behave as stub for embedding shape
        // while holding the VRAM reservation so the admission gate is proven.
        _ = _scheduler;
        return await _fallback.EmbedAsync(face, cancellationToken).ConfigureAwait(false);
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
            catch (Exception ex) { _logger?.LogDebug(ex, "[SFaceOnnx] reservation dispose failed"); }
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
                    "[SFaceOnnx] model '{Path}' not found; embedding degrades to stub",
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
                    "[SFaceOnnx] VRAM reservation denied: {Err}; degrading to stub",
                    reservation.Error);
                Interlocked.Exchange(ref _state, -1);
                return;
            }

            _reservation = reservation.Value;
            _logger?.LogInformation(
                "[SFaceOnnx] VRAM reservation acquired ({Bytes:N0} bytes) — ONNX session deferred to decoder-swap follow-up",
                EstimatedBytes);
            Interlocked.Exchange(ref _state, 1);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogWarning(ex, "[SFaceOnnx] init failed; degrading to stub");
            Interlocked.Exchange(ref _state, -1);
        }
        finally
        {
            _initLock.Release();
        }
    }
}
