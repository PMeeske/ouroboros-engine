// <copyright file="OnnxExpressionClassifier.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// FER+ ONNX expression classifier. Loads an FER+ <c>emotion-ferplus-8.onnx</c>
/// model via ORT DirectML, preprocesses each frame to <c>64x64</c> grayscale,
/// runs inference through the GPU scheduler at
/// <see cref="GpuTaskPriority.Background"/>, then maps the 8-class softmax
/// distribution to a 5D <see cref="AffectiveVector"/> via
/// <see cref="FerClassMapping"/>.
/// </summary>
/// <remarks>
/// The ORT session is shared across calls (singleton lifetime). Inference
/// failures are converted to <see cref="Result{T}.Failure(string)"/>; only
/// <see cref="OperationCanceledException"/> is allowed to propagate. Disposes
/// its <see cref="InferenceSession"/>, <see cref="SessionOptions"/>, and
/// <see cref="RunOptions"/> exactly once on
/// <see cref="DisposeAsync"/>.
/// </remarks>
public sealed class OnnxExpressionClassifier : IExpressionClassifier, IAsyncDisposable
{
    private const int InputDim = 64; // FER+ accepts 64x64 grayscale.
    private const long WorkingSetBytes = 32L * 1024 * 1024; // ~32MB activation working set.
    private static readonly TimeSpan ScheduleMaxLatency = TimeSpan.FromMilliseconds(150);

    private readonly InferenceSession _session;
    private readonly SessionOptions _sessionOptions;
    private readonly RunOptions _runOptions;
    private readonly GpuScheduler _scheduler;
    private readonly ILogger<OnnxExpressionClassifier>? _logger;
    private readonly string _inputName;
    private readonly string _outputName;
    private readonly long _modelFileSizeBytes;
    private readonly long _estimatedVramBytes;
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnnxExpressionClassifier"/>
    /// class by loading an FER+ ONNX model from disk and configuring an ORT
    /// DirectML session.
    /// </summary>
    /// <param name="modelPath">Absolute path to the FER+ <c>emotion-ferplus-8.onnx</c> model file.</param>
    /// <param name="scheduler">GPU scheduler used to mediate every inference dispatch.</param>
    /// <param name="logger">Optional logger for startup + diagnostic output.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modelPath"/> is null/empty/whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scheduler"/> is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the ONNX model file is missing.</exception>
    public OnnxExpressionClassifier(
        string modelPath,
        GpuScheduler scheduler,
        ILogger<OnnxExpressionClassifier>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentNullException.ThrowIfNull(scheduler);

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"FER+ ONNX model not found at '{modelPath}'", modelPath);
        }

        _scheduler = scheduler;
        _logger = logger;
        _modelFileSizeBytes = new FileInfo(modelPath).Length;
        _estimatedVramBytes = _modelFileSizeBytes + WorkingSetBytes;

        _sessionOptions = CreateSessionOptions(logger);
        _session = new InferenceSession(modelPath, _sessionOptions);
        _runOptions = new RunOptions();

        _inputName = _session.InputMetadata.Keys.First();
        _outputName = _session.OutputMetadata.Keys.First();

        logger?.LogInformation(
            "OnnxExpressionClassifier loaded: path={ModelPath} sizeMB={SizeMB:F1} input={Input} output={Output} vramEstMB={VramMB:F1}",
            modelPath,
            _modelFileSizeBytes / (1024.0 * 1024.0),
            _inputName,
            _outputName,
            _estimatedVramBytes / (1024.0 * 1024.0));
    }

    /// <inheritdoc/>
    public async Task<Result<AffectiveVector>> ClassifyAsync(
        FrameBuffer frame,
        CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return Result<AffectiveVector>.Failure("classifier disposed");
        }

        if (frame is null)
        {
            return Result<AffectiveVector>.Failure("frame null");
        }

        if (frame.Rgba is null || frame.Width <= 0 || frame.Height <= 0)
        {
            return Result<AffectiveVector>.Failure(
                $"invalid frame (W={frame.Width}, H={frame.Height}, rgba={frame.Rgba?.Length ?? 0})");
        }

        int expected = frame.Width * frame.Height * 4;
        if (frame.Rgba.Length != expected)
        {
            return Result<AffectiveVector>.Failure(
                $"rgba length mismatch (got {frame.Rgba.Length}, expected {expected})");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Preprocessing is CPU-cheap (64x64 = 4096 destination samples); do it
        // outside the scheduler delegate so the GPU slice contains only the
        // ORT.Run call.
        float[] inputBuffer;
        try
        {
            inputBuffer = PreprocessGrayscale64(frame);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Preprocessing failure is recoverable
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogDebug(ex, "OnnxExpressionClassifier preprocessing failed");
            return Result<AffectiveVector>.Failure("preprocess: " + ex.Message);
        }

        var requirements = new GpuResourceRequirements(
            EstimatedVramBytes: _estimatedVramBytes,
            RequiresExclusiveAccess: false,
            MaxLatency: ScheduleMaxLatency);

        try
        {
            AffectiveVector vector = await _scheduler.ScheduleAsync(
                GpuTaskPriority.Background,
                requirements,
                () => RunInferenceCore(inputBuffer),
                cancellationToken).ConfigureAwait(false);

            return Result<AffectiveVector>.Success(vector);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            // GpuScheduler signals MaxLatency exhaustion as TimeoutException —
            // expected back-pressure under heavy GPU load, log Debug only.
            _logger?.LogDebug(ex, "OnnxExpressionClassifier scheduler latency exceeded");
            return Result<AffectiveVector>.Failure("scheduler timeout");
        }
        catch (InvalidOperationException ex)
        {
            // GpuScheduler raises this when paused under VRAM pressure.
            _logger?.LogDebug(ex, "OnnxExpressionClassifier scheduler paused");
            return Result<AffectiveVector>.Failure("scheduler paused");
        }
        catch (InsufficientMemoryException ex)
        {
            _logger?.LogDebug(ex, "OnnxExpressionClassifier vram overcommit");
            return Result<AffectiveVector>.Failure("vram overcommit");
        }
#pragma warning disable CA1031 // Inference failure is recoverable
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogDebug(ex, "OnnxExpressionClassifier inference failed");
            return Result<AffectiveVector>.Failure("infer: " + ex.Message);
        }
    }

    /// <summary>
    /// Disposes the underlying ORT session, session options, and run options
    /// exactly once. Subsequent calls are no-ops.
    /// </summary>
    /// <returns>A completed <see cref="ValueTask"/>.</returns>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return ValueTask.CompletedTask;
        }

        _runOptions.Dispose();
        _session.Dispose();
        _sessionOptions.Dispose();
        return ValueTask.CompletedTask;
    }

    private AffectiveVector RunInferenceCore(float[] inputBuffer)
    {
        var inputTensor = new DenseTensor<float>(
            inputBuffer, new[] { 1, 1, InputDim, InputDim });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, inputTensor),
        };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs =
            _session.Run(inputs);

        foreach (DisposableNamedOnnxValue output in outputs)
        {
            if (output.Name != _outputName)
            {
                continue;
            }

            var logitsTensor = output.AsTensor<float>();
            if (logitsTensor.Length != FerClassMapping.ClassCount)
            {
                return AffectiveVector.Neutral;
            }

            Span<float> logits = stackalloc float[FerClassMapping.ClassCount];
            for (int i = 0; i < FerClassMapping.ClassCount; i++)
            {
                logits[i] = logitsTensor.GetValue(i);
            }

            Span<float> probs = stackalloc float[FerClassMapping.ClassCount];
            Softmax(logits, probs);
            return FerClassMapping.Map(probs);
        }

        return AffectiveVector.Neutral;
    }

    // Bilinear-resampled 64x64 grayscale. RGBA in [0,255] uint8 → grayscale via
    // luma weights → output float32 in [0,255] (FER+ takes raw pixel values, NOT
    // normalized to [0,1]).
    private static float[] PreprocessGrayscale64(FrameBuffer frame)
    {
        int srcW = frame.Width;
        int srcH = frame.Height;
        byte[] rgba = frame.Rgba;
        var output = new float[InputDim * InputDim];

        // Map each destination pixel to its source pixel via nearest-neighbor.
        // Bilinear would be marginally higher quality but the avatar render is
        // already a clean face crop — nearest is fine and cheap. Swap in
        // bilinear later if FER+ accuracy looks poor in production.
        float xScale = (float)srcW / InputDim;
        float yScale = (float)srcH / InputDim;

        for (int dy = 0; dy < InputDim; dy++)
        {
            int sy = Math.Min((int)(dy * yScale), srcH - 1);
            for (int dx = 0; dx < InputDim; dx++)
            {
                int sx = Math.Min((int)(dx * xScale), srcW - 1);
                int srcIdx = ((sy * srcW) + sx) * 4;

                byte r = rgba[srcIdx];
                byte g = rgba[srcIdx + 1];
                byte b = rgba[srcIdx + 2];

                // BT.601 luma — standard grayscale conversion.
                float luma = (0.299f * r) + (0.587f * g) + (0.114f * b);
                output[(dy * InputDim) + dx] = luma; // already in [0,255]
            }
        }

        return output;
    }

    private static void Softmax(ReadOnlySpan<float> logits, Span<float> probabilities)
    {
        float max = float.NegativeInfinity;
        for (int i = 0; i < logits.Length; i++)
        {
            if (logits[i] > max)
            {
                max = logits[i];
            }
        }

        float sum = 0f;
        for (int i = 0; i < logits.Length; i++)
        {
            probabilities[i] = MathF.Exp(logits[i] - max);
            sum += probabilities[i];
        }

        if (sum <= 0f)
        {
            sum = 1f;
        }

        for (int i = 0; i < probabilities.Length; i++)
        {
            probabilities[i] /= sum;
        }
    }

    private static SessionOptions CreateSessionOptions(ILogger? logger)
    {
        var opts = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
        };

        try
        {
            opts.AppendExecutionProvider_DML(0);
            opts.AddSessionConfigEntry("session.disable_mem_pattern", "1");
            logger?.LogInformation("OnnxExpressionClassifier: DirectML EP enabled");
        }
#pragma warning disable CA1031 // DirectML availability is opaque
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger?.LogWarning(ex, "OnnxExpressionClassifier: DirectML unavailable — using CPU EP");
        }

        return opts;
    }
}
