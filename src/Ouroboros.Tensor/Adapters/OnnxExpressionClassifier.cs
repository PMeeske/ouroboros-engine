// <copyright file="OnnxExpressionClassifier.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Ouroboros.Tensor.Orchestration;

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
/// The ORT session is created lazily on first inference. If the model file is
/// missing or session creation fails, all calls degrade to
/// <see cref="Engram{T}.Empty"/>.
/// </remarks>
public sealed class OnnxExpressionClassifier : IExpressionClassifier, IAsyncDisposable
{
    private const int InputDim = 64;
    private const long WorkingSetBytes = 32L * 1024 * 1024;
    private static readonly TimeSpan ScheduleMaxLatency = TimeSpan.FromMilliseconds(150);

    private readonly string _modelPath;
    private readonly GpuScheduler _scheduler;
    private readonly ILogger<OnnxExpressionClassifier>? _logger;
    private readonly long _estimatedVramBytes;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private InferenceSession? _session;
    private SessionOptions? _sessionOptions;
    private RunOptions? _runOptions;
    private string? _inputName;
    private string? _outputName;
    private int _state; // 0 = uninitialised, 1 = ready, -1 = fallback
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnnxExpressionClassifier"/>
    /// class. The model file is NOT loaded eagerly — first inference triggers
    /// lazy initialisation so missing files degrade gracefully.
    /// </summary>
    /// <param name="modelPath">Absolute path to the FER+ ONNX model file.</param>
    /// <param name="scheduler">GPU scheduler used to mediate every inference dispatch.</param>
    /// <param name="logger">Optional logger for startup + diagnostic output.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modelPath"/> is null/empty/whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scheduler"/> is null.</exception>
    public OnnxExpressionClassifier(
        string modelPath,
        GpuScheduler scheduler,
        ILogger<OnnxExpressionClassifier>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentNullException.ThrowIfNull(scheduler);

        _modelPath = modelPath;
        _scheduler = scheduler;
        _logger = logger;
        long fileSize = File.Exists(modelPath) ? new FileInfo(modelPath).Length : 0L;
        _estimatedVramBytes = fileSize + WorkingSetBytes;
    }

    /// <inheritdoc/>
    public async Task<Engram<AffectiveVector>> ClassifyAsync(
        FrameBuffer frame,
        CancellationToken cancellationToken)
    {
        if (_disposed || frame is null || frame.Rgba is null || frame.Width <= 0 || frame.Height <= 0)
        {
            return Engram<AffectiveVector>.Empty();
        }

        int expected = frame.Width * frame.Height * 4;
        if (frame.Rgba.Length != expected)
        {
            return Engram<AffectiveVector>.Empty();
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (_state == 0)
        {
            await EnsureInitialisedAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_state != 1 || _session is null)
        {
            return Engram<AffectiveVector>.Empty();
        }

        float[] inputBuffer;
        try
        {
            inputBuffer = PreprocessGrayscale64(frame);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogDebug(ex, "[FER+] preprocessing failed");
            return Engram<AffectiveVector>.Empty();
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
                () => Task.FromResult(RunInferenceCore(inputBuffer)),
                cancellationToken).ConfigureAwait(false);

            return Engram<AffectiveVector>.Create(
                vector,
                temporalContext: DateTimeOffset.UtcNow,
                somaticValence: Math.Clamp(vector.Valence, -1f, 1f),
                associativeLinks: Array.Empty<Guid>(),
                identityWeight: Math.Clamp(vector.Confidence, 0f, 1f));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            _logger?.LogDebug(ex, "[FER+] scheduler latency exceeded");
            return Engram<AffectiveVector>.Empty();
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogDebug(ex, "[FER+] scheduler paused");
            return Engram<AffectiveVector>.Empty();
        }
        catch (InsufficientMemoryException ex)
        {
            _logger?.LogDebug(ex, "[FER+] vram overcommit");
            return Engram<AffectiveVector>.Empty();
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogDebug(ex, "[FER+] inference failed");
            return Engram<AffectiveVector>.Empty();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _runOptions?.Dispose();
        _session?.Dispose();
        _sessionOptions?.Dispose();
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
                    "[FER+] model '{Path}' not found; classifier degrades to empty engrams",
                    _modelPath);
                Interlocked.Exchange(ref _state, -1);
                return;
            }

            SessionOptions opts = CreateSessionOptions(_logger);
            try
            {
                _session = new InferenceSession(_modelPath, opts);
                _sessionOptions = opts;
                _runOptions = new RunOptions();
                _inputName = _session.InputMetadata.Keys.First();
                _outputName = _session.OutputMetadata.Keys.First();

                _logger?.LogInformation(
                    "[FER+] session ready: input={Input} output={Output} vramEstMB={VramMB:F1}",
                    _inputName,
                    _outputName,
                    _estimatedVramBytes / (1024.0 * 1024.0));
                Interlocked.Exchange(ref _state, 1);
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                opts.Dispose();
                _logger?.LogWarning(ex, "[FER+] session creation failed; degrading to empty engrams");
                Interlocked.Exchange(ref _state, -1);
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogWarning(ex, "[FER+] init failed; degrading to empty engrams");
            Interlocked.Exchange(ref _state, -1);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private AffectiveVector RunInferenceCore(float[] inputBuffer)
    {
        InferenceSession? session = _session;
        string? inputName = _inputName;
        string? outputName = _outputName;
        if (session is null || inputName is null || outputName is null)
        {
            return AffectiveVector.Neutral;
        }

        var inputTensor = new DenseTensor<float>(
            inputBuffer, new[] { 1, 1, InputDim, InputDim });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputName, inputTensor),
        };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs =
            session.Run(inputs);

        foreach (DisposableNamedOnnxValue output in outputs)
        {
            if (output.Name != outputName)
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

    private static float[] PreprocessGrayscale64(FrameBuffer frame)
    {
        int srcW = frame.Width;
        int srcH = frame.Height;
        byte[] rgba = frame.Rgba;
        var output = new float[InputDim * InputDim];

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

                float luma = (0.299f * r) + (0.587f * g) + (0.114f * b);
                output[(dy * InputDim) + dx] = luma;
            }
        }

        return output;
    }

    private static void Softmax(ReadOnlySpan<float> logits, Span<float> probabilities)
    {
        float max = float.NegativeInfinity;
        for (int i = 0; i < logits.Length; i++)
        {
            if (logits[i] > max) max = logits[i];
        }

        float sum = 0f;
        for (int i = 0; i < logits.Length; i++)
        {
            probabilities[i] = MathF.Exp(logits[i] - max);
            sum += probabilities[i];
        }

        if (sum <= 0f) sum = 1f;

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
            logger?.LogInformation("[FER+] DirectML EP enabled");
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger?.LogWarning(ex, "[FER+] DirectML unavailable — using CPU EP");
        }

        return opts;
    }
}
