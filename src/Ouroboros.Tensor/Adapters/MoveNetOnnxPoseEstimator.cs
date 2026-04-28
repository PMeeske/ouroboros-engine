// <copyright file="MoveNetOnnxPoseEstimator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Orchestration;

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Phase 240 MoveNet Thunder ONNX pose estimator with real inference body
/// (v55.0 follow-up). Produces 17 body keypoints from a frame; falls back to
/// <see cref="StubPoseEstimator"/> when the model file is missing, admission
/// is denied, or inference fails.
/// </summary>
/// <remarks>
/// MoveNet Thunder takes <c>[1, 256, 256, 3]</c> uint8 NHWC input and emits
/// <c>[1, 1, 17, 3]</c> where each row is <c>(y, x, confidence)</c> normalized
/// to <c>[0, 1]</c> against the letterboxed 256×256 input. This decoder
/// pad-and-resizes the source frame into a centered square, inverts the
/// mapping, and returns keypoints in source-pixel coordinates.
/// </remarks>
public sealed class MoveNetOnnxPoseEstimator : IPoseEstimator, IAsyncDisposable
{
    /// <summary>Conservative VRAM estimate for MoveNet Thunder FP16 (~25 MiB).</summary>
    public const long EstimatedBytes = 28L * 1024L * 1024L;

    /// <summary>Stable model identifier for <see cref="IPerceptionVramBudget"/>.</summary>
    public const string ModelId = "movenet";

    private const int InputDim = 256;
    private const int KeypointCount = 17;
    private static readonly TimeSpan ScheduleMaxLatency = TimeSpan.FromMilliseconds(250);

    private readonly string _modelPath;
    private readonly IPerceptionVramBudget _budget;
    private readonly GpuScheduler _scheduler;
    private readonly StubPoseEstimator _fallback;
    private readonly ILogger<MoveNetOnnxPoseEstimator>? _logger;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IVramReservation? _reservation;
    private InferenceSession? _session;
    private SessionOptions? _sessionOptions;
    private RunOptions? _runOptions;
    private string? _inputName;
    private string? _outputName;
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
        if (_disposed || frame is null || frame.Rgba is null)
        {
            return null;
        }

        if (frame.Width <= 0 || frame.Height <= 0)
        {
            return null;
        }

        int expected = frame.Width * frame.Height * 4;
        if (frame.Rgba.Length != expected)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (_state == 0)
        {
            await EnsureInitialisedAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_state != 1 || _session is null)
        {
            return await _fallback.EstimateAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        byte[] inputBuffer;
        LetterboxParams letterbox;
        try
        {
            (inputBuffer, letterbox) = PreprocessLetterbox256(frame);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogDebug(ex, "[MoveNetOnnx] preprocessing failed");
            return await _fallback.EstimateAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        var requirements = new GpuResourceRequirements(
            EstimatedVramBytes: EstimatedBytes,
            RequiresExclusiveAccess: false,
            MaxLatency: ScheduleMaxLatency);

        try
        {
            PoseEstimate? estimate = await _scheduler.ScheduleAsync(
                GpuTaskPriority.Perception,
                requirements,
                () => Task.FromResult<PoseEstimate?>(RunInferenceCore(inputBuffer, letterbox)),
                cancellationToken).ConfigureAwait(false);

            return estimate ?? await _fallback.EstimateAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            _logger?.LogDebug(ex, "[MoveNetOnnx] scheduler latency exceeded");
            return null;
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogDebug(ex, "[MoveNetOnnx] scheduler paused");
            return null;
        }
        catch (InsufficientMemoryException ex)
        {
            _logger?.LogDebug(ex, "[MoveNetOnnx] vram overcommit");
            return null;
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogDebug(ex, "[MoveNetOnnx] inference failed");
            return null;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _runOptions?.Dispose();
        _session?.Dispose();
        _sessionOptions?.Dispose();

        IVramReservation? reservation = Interlocked.Exchange(ref _reservation, null);
        if (reservation is not null)
        {
            try
            {
                await reservation.DisposeAsync().ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "[MoveNetOnnx] reservation dispose failed");
            }
#pragma warning restore CA1031
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

            SessionOptions opts = CreateSessionOptions(_logger);
            try
            {
                _session = new InferenceSession(_modelPath, opts);
                _sessionOptions = opts;
                _runOptions = new RunOptions();
                _inputName = _session.InputMetadata.Keys.First();
                _outputName = _session.OutputMetadata.Keys.First();

                _logger?.LogInformation(
                    "[MoveNetOnnx] session ready: input={Input} output={Output} vramEstMB={VramMB:F1}",
                    _inputName,
                    _outputName,
                    EstimatedBytes / (1024.0 * 1024.0));
                Interlocked.Exchange(ref _state, 1);
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                opts.Dispose();
                _logger?.LogWarning(ex, "[MoveNetOnnx] session creation failed; degrading to stub");
                Interlocked.Exchange(ref _state, -1);
            }
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

    private PoseEstimate? RunInferenceCore(byte[] inputBuffer, LetterboxParams letterbox)
    {
        InferenceSession? session = _session;
        string? inputName = _inputName;
        string? outputName = _outputName;
        if (session is null || inputName is null || outputName is null)
        {
            return null;
        }

        // MoveNet's declared input dtype may be uint8 or int32 depending on the
        // export. DenseTensor<byte> handles the uint8 case directly; if the
        // session was exported as int32, the try/catch below surfaces the
        // InvalidCastException and we fall back to stub.
        var inputTensor = new DenseTensor<byte>(
            inputBuffer, new[] { 1, InputDim, InputDim, 3 });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputName, inputTensor),
        };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs = session.Run(inputs);
        foreach (DisposableNamedOnnxValue output in outputs)
        {
            if (!string.Equals(output.Name, outputName, StringComparison.Ordinal))
            {
                continue;
            }

            Microsoft.ML.OnnxRuntime.Tensors.Tensor<float> t = output.AsTensor<float>();
            if (t.Length != KeypointCount * 3)
            {
                return null;
            }

            var points = new PoseKeypoint[KeypointCount];
            float confSum = 0f;
            for (int k = 0; k < KeypointCount; k++)
            {
                int baseIdx = k * 3;
                float normY = t.GetValue(baseIdx);       // y-first MoveNet convention
                float normX = t.GetValue(baseIdx + 1);
                float conf = t.GetValue(baseIdx + 2);

                // Undo letterbox: (normX, normY) ∈ [0,1] against the padded 256×256.
                float paddedX = normX * InputDim;
                float paddedY = normY * InputDim;
                float srcX = (paddedX - letterbox.OffsetX) / letterbox.Scale;
                float srcY = (paddedY - letterbox.OffsetY) / letterbox.Scale;

                points[k] = new PoseKeypoint(srcX, srcY, conf);
                confSum += Math.Clamp(conf, 0f, 1f);
            }

            float overall = confSum / KeypointCount;
            return new PoseEstimate(points, overall);
        }

        return null;
    }

    private static (byte[] Data, LetterboxParams Params) PreprocessLetterbox256(FrameBuffer frame)
    {
        int srcW = frame.Width;
        int srcH = frame.Height;
        byte[] rgba = frame.Rgba;

        // Compute the centered letterbox so the aspect ratio is preserved.
        float scale = MathF.Min((float)InputDim / srcW, (float)InputDim / srcH);
        int scaledW = Math.Max(1, (int)(srcW * scale));
        int scaledH = Math.Max(1, (int)(srcH * scale));
        int offsetX = (InputDim - scaledW) / 2;
        int offsetY = (InputDim - scaledH) / 2;

        var dst = new byte[InputDim * InputDim * 3]; // NHWC uint8

        // Bands outside the letterbox stay zero (black) which matches TF Hub
        // preprocessing. The body of the image is nearest-neighbour resized.
        float invScale = 1f / scale;
        for (int dy = 0; dy < scaledH; dy++)
        {
            int sy = Math.Min((int)(dy * invScale), srcH - 1);
            int outY = dy + offsetY;
            if (outY < 0 || outY >= InputDim)
            {
                continue;
            }

            for (int dx = 0; dx < scaledW; dx++)
            {
                int sx = Math.Min((int)(dx * invScale), srcW - 1);
                int outX = dx + offsetX;
                if (outX < 0 || outX >= InputDim)
                {
                    continue;
                }

                int srcIdx = ((sy * srcW) + sx) * 4;
                int dstIdx = ((outY * InputDim) + outX) * 3;
                dst[dstIdx] = rgba[srcIdx];
                dst[dstIdx + 1] = rgba[srcIdx + 1];
                dst[dstIdx + 2] = rgba[srcIdx + 2];
            }
        }

        return (dst, new LetterboxParams(scale, offsetX, offsetY));
    }

    private static SessionOptions CreateSessionOptions(ILogger? logger)
    {
        var opts = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
        };

        if (OrtEnv.Instance().GetAvailableProviders().Contains("DmlExecutionProvider"))
        {
            try
            {
                opts.AppendExecutionProvider_DML(0);
                opts.AddSessionConfigEntry("session.disable_mem_pattern", "1");
                logger?.LogInformation("[MoveNetOnnx] DirectML EP enabled");
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger?.LogWarning("[MoveNetOnnx] DirectML unavailable — using CPU EP ({Reason})", ex.Message);
            }
        }
        else
        {
            logger?.LogWarning("[MoveNetOnnx] DirectML execution provider not available — using CPU EP");
        }

        return opts;
    }

    private readonly record struct LetterboxParams(float Scale, int OffsetX, int OffsetY);
}
