// <copyright file="YuNetOnnxFaceDetector.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Globalization;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Orchestration;

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Phase 237 YuNet face detector with real ONNX inference body (v55.0 follow-up).
/// Reserves VRAM via <see cref="IPerceptionVramBudget"/> before session creation
/// and falls back to a single full-frame detection when the model file is missing,
/// admission is denied, or inference fails.
/// </summary>
/// <remarks>
/// Model input is fixed <c>[1, 3, 640, 640]</c> FP32; outputs are six triplets of
/// (cls, obj, bbox, kps) at strides 8, 16, 32. Boxes are decoded in anchor-free
/// FCOS-style: <c>(l, t, r, b)</c> distances from the grid cell centre, scaled by
/// stride. Final score is <c>sqrt(cls * obj)</c>; post-processed via simple
/// greedy NMS at IoU 0.3. Phase 237 consumers take the top detection only.
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

    private const int InputDim = 640;
    private const float ConfThreshold = 0.6f;
    private const float NmsIouThreshold = 0.3f;
    private const float PixelNormMean = 127.5f;
    private const float PixelNormScale = 1f / 128f;
    private static readonly int[] Strides = [8, 16, 32];
    private static readonly TimeSpan ScheduleMaxLatency = TimeSpan.FromMilliseconds(250);

    private readonly string _modelPath;
    private readonly IPerceptionVramBudget _budget;
    private readonly GpuScheduler _scheduler;
    private readonly ILogger<YuNetOnnxFaceDetector>? _logger;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IVramReservation? _reservation;
    private InferenceSession? _session;
    private SessionOptions? _sessionOptions;
    private RunOptions? _runOptions;
    private string? _inputName;
    private int _state; // 0 = uninitialised, 1 = ready (real session), -1 = fallback
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="YuNetOnnxFaceDetector"/> class.
    /// </summary>
    /// <param name="modelPath">Absolute path to <c>yunet_face_detection.onnx</c>.</param>
    /// <param name="budget">Perception-tier VRAM admission gate.</param>
    /// <param name="scheduler">Shared GPU scheduler for ONNX inference dispatch.</param>
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

        int expected = frame.Width * frame.Height * 4;
        if (frame.Rgba.Length != expected || frame.Width <= 0 || frame.Height <= 0)
        {
            return Array.Empty<FaceDetection>();
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (_state == 0)
        {
            await EnsureInitialisedAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_state != 1 || _session is null)
        {
            return FullFrameFallback(frame);
        }

        float[] inputBuffer;
        try
        {
            inputBuffer = PreprocessToChw640(frame);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Recoverable preprocessing error.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogDebug(ex, "[YuNetOnnx] preprocessing failed");
            return FullFrameFallback(frame);
        }

        var requirements = new GpuResourceRequirements(
            EstimatedVramBytes: EstimatedBytes,
            RequiresExclusiveAccess: false,
            MaxLatency: ScheduleMaxLatency);

        try
        {
            IReadOnlyList<FaceDetection> detections = await _scheduler.ScheduleAsync(
                GpuTaskPriority.Perception,
                requirements,
                () => Task.FromResult(RunInferenceCore(inputBuffer, frame.Width, frame.Height)),
                cancellationToken).ConfigureAwait(false);

            return detections;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            _logger?.LogDebug(ex, "[YuNetOnnx] scheduler latency exceeded");
            return FullFrameFallback(frame);
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogDebug(ex, "[YuNetOnnx] scheduler paused");
            return FullFrameFallback(frame);
        }
        catch (InsufficientMemoryException ex)
        {
            _logger?.LogDebug(ex, "[YuNetOnnx] vram overcommit");
            return FullFrameFallback(frame);
        }
#pragma warning disable CA1031 // Recoverable inference error.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogDebug(ex, "[YuNetOnnx] inference failed");
            return FullFrameFallback(frame);
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

            SessionOptions opts = CreateSessionOptions(_logger);
            try
            {
                _session = new InferenceSession(_modelPath, opts);
                _sessionOptions = opts;
                _runOptions = new RunOptions();
                _inputName = _session.InputMetadata.Keys.First();
                _logger?.LogInformation(
                    "[YuNetOnnx] session ready: input={Input} vramEstMB={VramMB:F1}",
                    _inputName,
                    EstimatedBytes / (1024.0 * 1024.0));
                Interlocked.Exchange(ref _state, 1);
            }
#pragma warning disable CA1031 // Session creation failure should degrade to stub.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                opts.Dispose();
                _logger?.LogWarning(ex, "[YuNetOnnx] session creation failed; degrading to full-frame fallback");
                Interlocked.Exchange(ref _state, -1);
            }
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

    private IReadOnlyList<FaceDetection> RunInferenceCore(float[] inputBuffer, int srcW, int srcH)
    {
        InferenceSession? session = _session;
        string? inputName = _inputName;
        if (session is null || inputName is null)
        {
            return Array.Empty<FaceDetection>();
        }

        var inputTensor = new DenseTensor<float>(
            inputBuffer, new[] { 1, 3, InputDim, InputDim });
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputName, inputTensor),
        };

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs = session.Run(inputs);

        // Collect the six output tensors per stride.
        var outputMap = new Dictionary<string, float[]>(StringComparer.Ordinal);
        foreach (DisposableNamedOnnxValue output in outputs)
        {
            Microsoft.ML.OnnxRuntime.Tensors.Tensor<float> t = output.AsTensor<float>();
            float[] copy = t.ToArray();
            outputMap[output.Name] = copy;
        }

        var candidates = new List<Candidate>(capacity: 32);
        foreach (int stride in Strides)
        {
            if (!outputMap.TryGetValue($"cls_{stride}", out float[]? cls) ||
                !outputMap.TryGetValue($"obj_{stride}", out float[]? obj) ||
                !outputMap.TryGetValue($"bbox_{stride}", out float[]? bbox) ||
                !outputMap.TryGetValue($"kps_{stride}", out float[]? kps))
            {
                continue;
            }

            DecodeStride(stride, cls, obj, bbox, kps, srcW, srcH, candidates);
        }

        if (candidates.Count == 0)
        {
            return Array.Empty<FaceDetection>();
        }

        candidates.Sort(static (a, b) => b.Score.CompareTo(a.Score));
        List<Candidate> kept = Nms(candidates, NmsIouThreshold);

        // Phase 237 consumers take top-1.
        if (kept.Count == 0)
        {
            return Array.Empty<FaceDetection>();
        }

        Candidate top = kept[0];
        return
        [
            new FaceDetection(
                X: top.X,
                Y: top.Y,
                Width: top.W,
                Height: top.H,
                Confidence: top.Score,
                Landmarks5: top.Landmarks),
        ];
    }

    private static void DecodeStride(
        int stride,
        float[] cls,
        float[] obj,
        float[] bbox,
        float[] kps,
        int srcW,
        int srcH,
        List<Candidate> candidates)
    {
        int featDim = InputDim / stride;
        int cells = featDim * featDim;
        if (cls.Length != cells || obj.Length != cells ||
            bbox.Length != cells * 4 || kps.Length != cells * 10)
        {
            return;
        }

        float scaleX = (float)srcW / InputDim;
        float scaleY = (float)srcH / InputDim;

        for (int idx = 0; idx < cells; idx++)
        {
            float clsScore = cls[idx];
            float objScore = obj[idx];
            float score = MathF.Sqrt(Math.Clamp(clsScore, 0f, 1f) * Math.Clamp(objScore, 0f, 1f));
            if (score < ConfThreshold)
            {
                continue;
            }

            int row = idx / featDim;
            int col = idx % featDim;
            float cxGrid = (col + 0.5f) * stride;
            float cyGrid = (row + 0.5f) * stride;

            int b = idx * 4;
            float l = bbox[b] * stride;
            float t = bbox[b + 1] * stride;
            float r = bbox[b + 2] * stride;
            float bo = bbox[b + 3] * stride;

            // Rectify to resized-image space then back to source pixels.
            float x1 = (cxGrid - l) * scaleX;
            float y1 = (cyGrid - t) * scaleY;
            float x2 = (cxGrid + r) * scaleX;
            float y2 = (cyGrid + bo) * scaleY;

            int ix = (int)MathF.Max(0, MathF.Floor(x1));
            int iy = (int)MathF.Max(0, MathF.Floor(y1));
            int iw = (int)MathF.Min(srcW - ix, MathF.Ceiling(x2 - x1));
            int ih = (int)MathF.Min(srcH - iy, MathF.Ceiling(y2 - y1));
            if (iw <= 0 || ih <= 0)
            {
                continue;
            }

            int kpBase = idx * 10;
            var landmarks = new float[10];
            for (int p = 0; p < 5; p++)
            {
                float px = (cxGrid + (kps[kpBase + (p * 2)] * stride)) * scaleX;
                float py = (cyGrid + (kps[kpBase + (p * 2) + 1] * stride)) * scaleY;
                landmarks[p * 2] = px;
                landmarks[(p * 2) + 1] = py;
            }

            candidates.Add(new Candidate(ix, iy, iw, ih, score, landmarks));
        }
    }

    private static List<Candidate> Nms(List<Candidate> cands, float iouThresh)
    {
        var kept = new List<Candidate>(capacity: Math.Min(8, cands.Count));
        bool[] suppressed = new bool[cands.Count];
        for (int i = 0; i < cands.Count; i++)
        {
            if (suppressed[i])
            {
                continue;
            }

            kept.Add(cands[i]);
            for (int j = i + 1; j < cands.Count; j++)
            {
                if (suppressed[j])
                {
                    continue;
                }

                if (Iou(cands[i], cands[j]) >= iouThresh)
                {
                    suppressed[j] = true;
                }
            }
        }

        return kept;
    }

    private static float Iou(in Candidate a, in Candidate b)
    {
        int ax2 = a.X + a.W;
        int ay2 = a.Y + a.H;
        int bx2 = b.X + b.W;
        int by2 = b.Y + b.H;

        int ix1 = Math.Max(a.X, b.X);
        int iy1 = Math.Max(a.Y, b.Y);
        int ix2 = Math.Min(ax2, bx2);
        int iy2 = Math.Min(ay2, by2);

        int iw = Math.Max(0, ix2 - ix1);
        int ih = Math.Max(0, iy2 - iy1);
        int inter = iw * ih;
        int union = (a.W * a.H) + (b.W * b.H) - inter;
        return union <= 0 ? 0f : (float)inter / union;
    }

    // Letterbox to 640x640 CHW FP32 with YuNet normalization: (pixel - 127.5) / 128.
    private static float[] PreprocessToChw640(FrameBuffer frame)
    {
        int srcW = frame.Width;
        int srcH = frame.Height;
        byte[] rgba = frame.Rgba;
        var output = new float[3 * InputDim * InputDim];

        // Simple nearest-neighbour resize; the source is already a face-crop-friendly
        // frame from the camera sensor — higher-order resampling would add CPU cost
        // without meaningful accuracy gain for YuNet's coarse-grid decoding.
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
                logger?.LogInformation("[YuNetOnnx] DirectML EP enabled");
            }
#pragma warning disable CA1031 // DirectML availability is opaque.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger?.LogWarning("[YuNetOnnx] DirectML unavailable — using CPU EP ({Reason})", ex.Message);
            }
        }
        else
        {
            logger?.LogWarning("[YuNetOnnx] DirectML execution provider not available — using CPU EP");
        }

        return opts;
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

    private readonly struct Candidate(int x, int y, int w, int h, float score, float[] landmarks)
    {
        public int X { get; } = x;

        public int Y { get; } = y;

        public int W { get; } = w;

        public int H { get; } = h;

        public float Score { get; } = score;

        public float[] Landmarks { get; } = landmarks;

        public override string ToString()
            => string.Format(
                CultureInfo.InvariantCulture,
                "({0},{1},{2}x{3},s={4:F3})",
                X, Y, W, H, Score);
    }
}
