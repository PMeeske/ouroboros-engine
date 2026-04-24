// <copyright file="YuNetOnnxFaceDetector.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Abstractions.Monads;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Orchestration;

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Phase 237 YuNet face detector scaffold. Registers its VRAM reservation via
/// <see cref="IPerceptionVramBudget"/> before any ONNX session would be created
/// and falls back to a full-frame detection when the model file is missing or
/// admission is denied. The real ONNX inference body is wired up by the operator
/// dropping the model at <c>data/iaret-model/onnx/yunet_face_detection.onnx</c>
/// — the scaffold follows the same "plumb first, validate later" pattern as
/// <c>OnnxExpressionClassifier</c> (260424-2bv).
/// </summary>
/// <remarks>
/// <para>
/// Why a scaffold, not a full ONNX implementation yet? YuNet's output tensor
/// layout is model-variant specific (OpenCV Zoo vs. libfacedetection vs. ONNX
/// export). Shipping a validated decoder without the real model file on this
/// hardware would hide integration bugs. The scaffold hands downstream Phase 238
/// a VRAM-budgeted path-to-ONNX so the admission gate is proven today and the
/// decoder swap in Phase 237-follow-up is a localized change.
/// </para>
/// <para>
/// Invariants:
/// <list type="bullet">
///   <item>The VRAM reservation is acquired lazily on first <see cref="DetectAsync"/>
///         and held for the detector's lifetime.</item>
///   <item>Budget denial ⇒ the detector degrades to full-frame behaviour
///         (never starves the pipeline).</item>
///   <item>Model path missing ⇒ same full-frame fallback; single INFO log at
///         construction to surface the configuration mismatch.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class YuNetOnnxFaceDetector : IFaceDetector, IAsyncDisposable
{
    /// <summary>
    /// Best-effort VRAM estimate for YuNet (~4 MiB INT8 / ~14 MiB FP32). Using
    /// the upper bound keeps the budget conservative. Source: OpenCV Zoo README.
    /// </summary>
    public const long EstimatedBytes = 16L * 1024L * 1024L;

    /// <summary>Stable model identifier for <see cref="IPerceptionVramBudget"/>.</summary>
    public const string ModelId = "yunet";

    private readonly string _modelPath;
    private readonly IPerceptionVramBudget _budget;
    private readonly GpuScheduler _scheduler;
    private readonly ILogger<YuNetOnnxFaceDetector>? _logger;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IVramReservation? _reservation;
    private int _state; // 0 = uninitialised, 1 = ready (reservation acquired, scaffold), -1 = fallback
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="YuNetOnnxFaceDetector"/> class.
    /// </summary>
    /// <param name="modelPath">Absolute path to <c>yunet_face_detection.onnx</c>.</param>
    /// <param name="budget">Perception-tier VRAM admission gate.</param>
    /// <param name="scheduler">Shared GPU scheduler for future ONNX inference dispatch.</param>
    /// <param name="logger">Optional logger.</param>
    public YuNetOnnxFaceDetector(
        string modelPath,
        IPerceptionVramBudget budget,
        GpuScheduler scheduler,
        ILogger<YuNetOnnxFaceDetector>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(scheduler);
        _modelPath = modelPath;
        _budget = budget;
        _scheduler = scheduler;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FaceDetection>> DetectAsync(
        FrameBuffer frame,
        CancellationToken cancellationToken)
    {
        if (_disposed || frame is null || frame.Rgba is null || frame.Rgba.Length < 16)
        {
            return Array.Empty<FaceDetection>();
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (_state == 0)
        {
            await EnsureInitialisedAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_state != 1)
        {
            return FullFrameFallback(frame);
        }

        // TODO(Phase 237-follow-up): real YuNet ONNX inference dispatched through
        // _scheduler at GpuPriorityClass.Perception. Until the model file is
        // dropped and the output-tensor decoder is validated, behave as the stub:
        // return a single full-frame detection. Keeps the VRAM reservation proven.
        _ = _scheduler; // suppress unused warning until decoder ships
        return FullFrameFallback(frame);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        IVramReservation? reservation = Interlocked.Exchange(ref _reservation, null);
        if (reservation is not null)
        {
            try
            {
                await reservation.DisposeAsync().ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Dispose path; swallow and continue.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger?.LogDebug(ex, "[YuNetOnnx] reservation dispose failed");
            }
        }

        _initLock.Dispose();
    }

    private async Task EnsureInitialisedAsync(CancellationToken ct)
    {
        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_state != 0)
            {
                return;
            }

            if (!File.Exists(_modelPath))
            {
                _logger?.LogInformation(
                    "[YuNetOnnx] model file '{Path}' not found; face detection degrades to full-frame fallback",
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
                    "[YuNetOnnx] VRAM reservation denied: {Diagnostic}; degrading to full-frame fallback",
                    reservation.Error);
                Interlocked.Exchange(ref _state, -1);
                return;
            }

            _reservation = reservation.Value;

            // TODO(Phase 237-follow-up): construct InferenceSession here and cache.
            // Leaving the session slot empty deliberately — the VRAM gate is the
            // integration point that needed to land now.
            _logger?.LogInformation(
                "[YuNetOnnx] VRAM reservation acquired ({Bytes:N0} bytes) — ONNX session deferred to decoder-swap follow-up",
                EstimatedBytes);

            Interlocked.Exchange(ref _state, 1);
        }
#pragma warning disable CA1031 // Init must never throw into the hot path.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogWarning(ex, "[YuNetOnnx] initialisation failed; degrading to full-frame fallback");
            Interlocked.Exchange(ref _state, -1);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static IReadOnlyList<FaceDetection> FullFrameFallback(FrameBuffer frame)
    {
        return
        [
            new FaceDetection(
                X: 0,
                Y: 0,
                Width: frame.Width,
                Height: frame.Height,
                Confidence: 1.0f,
                Landmarks5: null),
        ];
    }
}
