// <copyright file="SFaceOnnxEmbedder.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Orchestration;

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Phase 239 SFace ONNX face embedder with real inference body (v55.0 follow-up).
/// Produces a 128d L2-normalized embedding from a face-cropped RGBA
/// <see cref="FrameBuffer"/>. Reserves VRAM via <see cref="IPerceptionVramBudget"/>
/// before session creation; degrades to <see cref="StubFaceEmbedder"/> behaviour
/// when the model file is missing, admission is denied, or inference fails.
/// </summary>
/// <remarks>
/// Model input is <c>[1, 3, 112, 112]</c> CHW FP32 with normalization
/// <c>(pixel - 127.5) / 128</c> in channel order R, G, B. Output
/// <c>fc1 [1, 128]</c> is L2-normalized in-place so cosine similarity
/// reduces to a dot product in the identity registry.
/// </remarks>
public sealed class SFaceOnnxEmbedder : IFaceEmbedder, IAsyncDisposable
{
    /// <summary>
    /// Best-effort VRAM estimate for SFace (~30 MiB FP32). Conservative upper
    /// bound; source: OpenCV Zoo README.
    /// </summary>
    public const long EstimatedBytes = 32L * 1024L * 1024L;

    /// <summary>Stable model identifier for <see cref="IPerceptionVramBudget"/>.</summary>
    public const string ModelId = "sface";

    private const int InputDim = 112;
    private const float PixelNormMean = 127.5f;
    private const float PixelNormScale = 1f / 128f;
    private static readonly TimeSpan ScheduleMaxLatency = TimeSpan.FromMilliseconds(200);

    private readonly string _modelPath;
    private readonly IPerceptionVramBudget _budget;
    private readonly GpuScheduler _scheduler;
    private readonly StubFaceEmbedder _fallback;
    private readonly ILogger<SFaceOnnxEmbedder>? _logger;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IVramReservation? _reservation;
    private InferenceSession? _session;
    private SessionOptions? _sessionOptions;
    private RunOptions? _runOptions;
    private string? _inputName;
    private string? _outputName;
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
            return await _fallback.EmbedAsync(face, cancellationToken).ConfigureAwait(false);
        }

        float[] inputBuffer;
        try
        {
            inputBuffer = PreprocessToChw112(face);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Recoverable preprocessing failure.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogDebug(ex, "[SFaceOnnx] preprocessing failed");
            return await _fallback.EmbedAsync(face, cancellationToken).ConfigureAwait(false);
        }

        var requirements = new GpuResourceRequirements(
            EstimatedVramBytes: EstimatedBytes,
            RequiresExclusiveAccess: false,
            MaxLatency: ScheduleMaxLatency);

        try
        {
            float[]? embedding = await _scheduler.ScheduleAsync(
                GpuTaskPriority.Perception,
                requirements,
                () => Task.FromResult<float[]?>(RunInferenceCore(inputBuffer)),
                cancellationToken).ConfigureAwait(false);

            return embedding ?? await _fallback.EmbedAsync(face, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            _logger?.LogDebug(ex, "[SFaceOnnx] scheduler latency exceeded");
            return null;
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogDebug(ex, "[SFaceOnnx] scheduler paused");
            return null;
        }
        catch (InsufficientMemoryException ex)
        {
            _logger?.LogDebug(ex, "[SFaceOnnx] vram overcommit");
            return null;
        }
#pragma warning disable CA1031 // Recoverable inference failure.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogDebug(ex, "[SFaceOnnx] inference failed");
            return null;
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

            SessionOptions opts = CreateSessionOptions(_logger);
            try
            {
                _session = new InferenceSession(_modelPath, opts);
                _sessionOptions = opts;
                _runOptions = new RunOptions();

                // The SFace OpenCV Zoo export lists ~175 "inputs" — 174 of these are
                // model weights exported as initializers+inputs. Pick the single
                // real frame input by shape match ([1,3,112,112]); fall back to the
                // first name if the metadata is flat.
                _inputName = FindInputByShape(_session, 1, 3, InputDim, InputDim)
                    ?? _session.InputMetadata.Keys.First();
                _outputName = _session.OutputMetadata.Keys.First();

                _logger?.LogInformation(
                    "[SFaceOnnx] session ready: input={Input} output={Output} vramEstMB={VramMB:F1}",
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
                _logger?.LogWarning(ex, "[SFaceOnnx] session creation failed; degrading to stub");
                Interlocked.Exchange(ref _state, -1);
            }
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

    private float[]? RunInferenceCore(float[] inputBuffer)
    {
        InferenceSession? session = _session;
        string? inputName = _inputName;
        string? outputName = _outputName;
        if (session is null || inputName is null || outputName is null)
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
        foreach (DisposableNamedOnnxValue output in outputs)
        {
            if (!string.Equals(output.Name, outputName, StringComparison.Ordinal))
            {
                continue;
            }

            Microsoft.ML.OnnxRuntime.Tensors.Tensor<float> t = output.AsTensor<float>();
            if (t.Length != StubFaceEmbedder.EmbeddingDimensions)
            {
                return null;
            }

            var result = new float[StubFaceEmbedder.EmbeddingDimensions];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = t.GetValue(i);
            }

            // L2-normalize so cosine similarity == dot product in the identity registry.
            double normSq = 0;
            for (int i = 0; i < result.Length; i++)
            {
                normSq += result[i] * result[i];
            }

            float norm = (float)Math.Sqrt(normSq);
            if (norm > 1e-9f)
            {
                float inv = 1f / norm;
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] *= inv;
                }
            }

            return result;
        }

        return null;
    }

    // Resize to 112x112 CHW FP32 with SFace normalization: (pixel - 127.5) / 128,
    // channel order R, G, B. The input frame is the face crop produced by
    // IFaceDetector — callers should crop to the detector's bbox before calling.
    private static float[] PreprocessToChw112(FrameBuffer face)
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
                byte r = rgba[srcIdx];
                byte g = rgba[srcIdx + 1];
                byte b = rgba[srcIdx + 2];

                int dstBase = (dy * InputDim) + dx;
                output[dstBase] = (r - PixelNormMean) * PixelNormScale;
                output[plane + dstBase] = (g - PixelNormMean) * PixelNormScale;
                output[(plane * 2) + dstBase] = (b - PixelNormMean) * PixelNormScale;
            }
        }

        return output;
    }

    private static string? FindInputByShape(InferenceSession session, params int[] want)
    {
        foreach (KeyValuePair<string, NodeMetadata> kv in session.InputMetadata)
        {
            int[] dims = kv.Value.Dimensions;
            if (dims is null || dims.Length != want.Length)
            {
                continue;
            }

            bool match = true;
            for (int i = 0; i < dims.Length; i++)
            {
                if (dims[i] != want[i])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return kv.Key;
            }
        }

        return null;
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
            logger?.LogInformation("[SFaceOnnx] DirectML EP enabled");
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger?.LogWarning(ex, "[SFaceOnnx] DirectML unavailable — using CPU EP");
        }

        return opts;
    }
}
