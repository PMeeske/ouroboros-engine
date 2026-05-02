// <copyright file="MobileGazeOnnxEstimator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Orchestration;

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Phase 240 MobileGaze ONNX gaze estimator with real inference body (v55.0
/// follow-up). Produces <see cref="GazeEstimate"/> (yaw, pitch, confidence)
/// from a face-cropped frame; falls back to <see cref="StubGazeEstimator"/>
/// when the model file is missing, admission is denied, or inference fails.
/// </summary>
/// <remarks>
/// Actual model I/O (queried from the ONNX graph): input <c>[1, 3, 448, 448]</c>
/// CHW FP32 with ImageNet normalization; output two 90-bin classification
/// heads <c>yaw [1, 90]</c> + <c>pitch [1, 90]</c> in Gaze360 / MobileGaze
/// convention (bins span <c>-180°..180°</c> in 4° steps). Final angle is
/// <c>sum(softmax(logits) · idx) · 4° - 180°</c>, converted to radians.
/// Confidence is the max softmax probability averaged across the two heads.
/// </remarks>
public sealed class MobileGazeOnnxEstimator : IGazeEstimator, IAsyncDisposable
{
    /// <summary>MobileGaze VRAM estimate (~12 MiB FP32).</summary>
    public const long EstimatedBytes = 14L * 1024L * 1024L;

    /// <summary>Stable model identifier for <see cref="IPerceptionVramBudget"/>.</summary>
    public const string ModelId = "mobilegaze";

    private const int InputDim = 448;
    private const int BinCount = 90;
    private const float BinDegrees = 4f; // 90 bins × 4° = 360° span
    private const float BinOriginDegrees = -180f;
    private static readonly float[] ImageNetMean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] ImageNetStd = [0.229f, 0.224f, 0.225f];
    private static readonly TimeSpan ScheduleMaxLatency = TimeSpan.FromMilliseconds(250);

    private readonly string _modelPath;
    private readonly IPerceptionVramBudget _budget;
    private readonly GpuScheduler _scheduler;
    private readonly StubGazeEstimator _fallback;
    private readonly ILogger<MobileGazeOnnxEstimator>? _logger;
    private readonly ISharedOrtDmlSessionFactory? _sessionFactory;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IVramReservation? _reservation;
    private InferenceSession? _session;
    private SessionOptions? _sessionOptions;
    private RunOptions? _runOptions;
    private string? _inputName;
    private string? _yawOutput;
    private string? _pitchOutput;
    private int _state;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="MobileGazeOnnxEstimator"/> class.</summary>
    /// <param name="modelPath">Absolute path to the MobileGaze ONNX file.</param>
    /// <param name="budget">Perception VRAM admission gate.</param>
    /// <param name="scheduler">Shared GPU scheduler.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="sessionFactory">
    /// Phase 196.3-02: when supplied, ORT sessions bind to the same shared D3D12 device
    /// as the rasterizer. When null or unavailable, falls back to legacy local DML EP.
    /// </param>
    public MobileGazeOnnxEstimator(
        string modelPath,
        IPerceptionVramBudget budget,
        GpuScheduler scheduler,
        ILogger<MobileGazeOnnxEstimator>? logger = null,
        ISharedOrtDmlSessionFactory? sessionFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(scheduler);
        _modelPath = modelPath;
        _budget = budget;
        _scheduler = scheduler;
        _fallback = new StubGazeEstimator(null);
        _logger = logger;
        _sessionFactory = sessionFactory;
    }

    /// <inheritdoc/>
    public async Task<GazeEstimate?> EstimateAsync(FrameBuffer face, CancellationToken cancellationToken)
    {
        if (_disposed || face is null || face.Rgba is null)
        {
            return null;
        }

        if (face.Width <= 0 || face.Height <= 0)
        {
            return null;
        }

        int expected = face.Width * face.Height * 4;
        if (face.Rgba.Length != expected)
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
            return await _fallback.EstimateAsync(face, cancellationToken).ConfigureAwait(false);
        }

        float[] inputBuffer;
        try
        {
            inputBuffer = PreprocessToChw448(face);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogDebug(ex, "[MobileGazeOnnx] preprocessing failed");
            return await _fallback.EstimateAsync(face, cancellationToken).ConfigureAwait(false);
        }

        var requirements = new GpuResourceRequirements(
            EstimatedVramBytes: EstimatedBytes,
            RequiresExclusiveAccess: false,
            MaxLatency: ScheduleMaxLatency);

        try
        {
            GazeEstimate? estimate = await _scheduler.ScheduleAsync(
                GpuTaskPriority.Perception,
                requirements,
                () => Task.FromResult<GazeEstimate?>(RunInferenceCore(inputBuffer)),
                cancellationToken).ConfigureAwait(false);

            return estimate ?? await _fallback.EstimateAsync(face, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            _logger?.LogDebug(ex, "[MobileGazeOnnx] scheduler latency exceeded");
            return null;
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogDebug(ex, "[MobileGazeOnnx] scheduler paused");
            return null;
        }
        catch (InsufficientMemoryException ex)
        {
            _logger?.LogDebug(ex, "[MobileGazeOnnx] vram overcommit");
            return null;
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogDebug(ex, "[MobileGazeOnnx] inference failed");
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
                _logger?.LogDebug(ex, "[MobileGazeOnnx] reservation dispose failed");
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

            SessionOptions opts = CreateSessionOptionsViaFactoryOrLocal();
            try
            {
                _session = new InferenceSession(_modelPath, opts);
                _sessionOptions = opts;
                _runOptions = new RunOptions();
                _inputName = _session.InputMetadata.Keys.First();
                _yawOutput = FindOutput(_session, "yaw") ?? _session.OutputMetadata.Keys.First();
                _pitchOutput = FindOutput(_session, "pitch")
                    ?? _session.OutputMetadata.Keys.Skip(1).FirstOrDefault()
                    ?? _yawOutput;

                _logger?.LogInformation(
                    "[MobileGazeOnnx] session ready: input={Input} yaw={Yaw} pitch={Pitch} vramEstMB={VramMB:F1}",
                    _inputName,
                    _yawOutput,
                    _pitchOutput,
                    EstimatedBytes / (1024.0 * 1024.0));
                Interlocked.Exchange(ref _state, 1);
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                opts.Dispose();
                _logger?.LogWarning(ex, "[MobileGazeOnnx] session creation failed; degrading to stub");
                Interlocked.Exchange(ref _state, -1);
            }
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

    private GazeEstimate? RunInferenceCore(float[] inputBuffer)
    {
        InferenceSession? session = _session;
        string? inputName = _inputName;
        string? yawName = _yawOutput;
        string? pitchName = _pitchOutput;
        if (session is null || inputName is null || yawName is null || pitchName is null)
        {
            return null;
        }

        var inputTensor = new DenseTensor<float>(
            inputBuffer, new[] { 1, 3, InputDim, InputDim });
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputName, inputTensor),
        };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs = session.Run(inputs);

        float[]? yawLogits = null;
        float[]? pitchLogits = null;
        foreach (DisposableNamedOnnxValue output in outputs)
        {
            if (string.Equals(output.Name, yawName, StringComparison.Ordinal))
            {
                yawLogits = output.AsTensor<float>().ToArray();
            }
            else if (string.Equals(output.Name, pitchName, StringComparison.Ordinal))
            {
                pitchLogits = output.AsTensor<float>().ToArray();
            }
        }

        if (yawLogits is null || pitchLogits is null)
        {
            return null;
        }

        if (yawLogits.Length != BinCount || pitchLogits.Length != BinCount)
        {
            return null;
        }

        (float yawDeg, float yawConf) = DecodeBinned(yawLogits);
        (float pitchDeg, float pitchConf) = DecodeBinned(pitchLogits);

        float yawRad = yawDeg * MathF.PI / 180f;
        float pitchRad = pitchDeg * MathF.PI / 180f;
        float confidence = (yawConf + pitchConf) * 0.5f;

        return new GazeEstimate(yawRad, pitchRad, confidence);
    }

    // Standard Gaze360/MobileGaze angular regression from soft-classification:
    //   angle_deg = sum_i (softmax(logits)[i] * i) * BinDegrees + BinOriginDegrees
    // Confidence proxy = max softmax probability.
    private static (float AngleDegrees, float Confidence) DecodeBinned(float[] logits)
    {
        // Softmax.
        float max = float.NegativeInfinity;
        for (int i = 0; i < logits.Length; i++)
        {
            if (logits[i] > max)
            {
                max = logits[i];
            }
        }

        Span<float> probs = stackalloc float[BinCount];
        float sum = 0f;
        for (int i = 0; i < logits.Length; i++)
        {
            probs[i] = MathF.Exp(logits[i] - max);
            sum += probs[i];
        }

        if (sum <= 0f)
        {
            sum = 1f;
        }

        float expected = 0f;
        float peak = 0f;
        for (int i = 0; i < logits.Length; i++)
        {
            float p = probs[i] / sum;
            probs[i] = p;
            expected += p * i;
            if (p > peak)
            {
                peak = p;
            }
        }

        float degrees = (expected * BinDegrees) + BinOriginDegrees;
        return (degrees, peak);
    }

    // Resize to 448x448 CHW FP32 with ImageNet normalization (mean/std, RGB).
    // Pixel values first normalized to [0,1] then standardized per channel.
    private static float[] PreprocessToChw448(FrameBuffer face)
    {
        int srcW = face.Width;
        int srcH = face.Height;
        byte[] rgba = face.Rgba;
        var output = new float[3 * InputDim * InputDim];

        float xScale = (float)srcW / InputDim;
        float yScale = (float)srcH / InputDim;
        int plane = InputDim * InputDim;

        for (int dy = 0; dy < InputDim; dy++)
        {
            int sy = Math.Min((int)(dy * yScale), srcH - 1);
            for (int dx = 0; dx < InputDim; dx++)
            {
                int sx = Math.Min((int)(dx * xScale), srcW - 1);
                int srcIdx = ((sy * srcW) + sx) * 4;
                float r = rgba[srcIdx] / 255f;
                float g = rgba[srcIdx + 1] / 255f;
                float b = rgba[srcIdx + 2] / 255f;

                int dstBase = (dy * InputDim) + dx;
                output[dstBase] = (r - ImageNetMean[0]) / ImageNetStd[0];
                output[plane + dstBase] = (g - ImageNetMean[1]) / ImageNetStd[1];
                output[(plane * 2) + dstBase] = (b - ImageNetMean[2]) / ImageNetStd[2];
            }
        }

        return output;
    }

    private static string? FindOutput(InferenceSession session, string needle)
    {
        foreach (string name in session.OutputMetadata.Keys)
        {
            if (name.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>Probes whether the DirectML execution provider is actually functional.</summary>
    private static bool IsDirectMlAvailable()
    {
        try
        {
            using var probe = new SessionOptions();
            probe.AppendExecutionProvider_DML(0);
            return true;
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException or BadImageFormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Phase 196.3-02: prefer the shared-device factory; fall back to local DML EP only
    /// when the factory is absent or the shared device is unavailable.
    /// </summary>
    private SessionOptions CreateSessionOptionsViaFactoryOrLocal()
    {
        if (_sessionFactory is not null)
        {
            try
            {
                SessionOptions shared = _sessionFactory.CreateSessionOptions();
                _logger?.LogInformation("[MobileGazeOnnx] DML EP via shared D3D12 device factory");
                return shared;
            }
            catch (InvalidOperationException ex)
            {
                _logger?.LogWarning(
                    "[MobileGazeOnnx] Shared DML factory unavailable ({Reason}); falling back to local EP",
                    ex.Message);
            }
        }

        return CreateSessionOptionsLocal(_logger);
    }

    private static SessionOptions CreateSessionOptionsLocal(ILogger? logger)
    {
        var opts = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
        };

        if (IsDirectMlAvailable())
        {
            try
            {
                opts.AppendExecutionProvider_DML(0);
                opts.AddSessionConfigEntry("session.disable_mem_pattern", "1");
                logger?.LogInformation("[MobileGazeOnnx] DirectML EP enabled (legacy local path)");
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger?.LogWarning("[MobileGazeOnnx] DirectML unavailable — using CPU EP ({Reason})", ex.Message);
            }
        }
        else
        {
            logger?.LogWarning("[MobileGazeOnnx] DirectML execution provider not available — using CPU EP");
        }

        return opts;
    }
}
