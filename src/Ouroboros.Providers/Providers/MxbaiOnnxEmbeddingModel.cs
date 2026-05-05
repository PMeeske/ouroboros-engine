// <copyright file="MxbaiOnnxEmbeddingModel.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Numerics.Tensors;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using Ouroboros.Abstractions.Core;
using Ouroboros.Providers.Meai;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Orchestration;
using IEmbeddingModel = Ouroboros.Domain.IEmbeddingModel;

namespace Ouroboros.Providers;

/// <summary>
/// Real transformer-based embedding generator running mxbai-embed-large-v1 (BERT-large
/// 1024-dim) on DirectML through the shared D3D12 ORT factory and the Phase 196.5 GPU
/// scheduler. Uses Microsoft.ML.Tokenizers' <see cref="BertTokenizer"/> for WordPiece
/// tokenization, attention-mask-weighted mean pooling, and L2 normalization.
/// </summary>
/// <remarks>
/// <para>
/// Replaces the placeholder character-level tokenizer + naive divide-by-seq-length
/// pooling in <see cref="TensorEmbeddingModel"/>'s ONNX branch. Produces the same
/// 1024-dim vector space that Ollama's <c>mxbai-embed-large</c> serves today, so
/// existing Qdrant collections (29 cognitive collections seeded under v51.0) require
/// no re-embedding when the embedding provider strategy chain (Phase 272-02) selects
/// this implementation.
/// </para>
/// <para>
/// <b>Tenant registration:</b> when an <see cref="IGpuScheduler"/> is supplied, the
/// constructor registers the model as the <c>"Embedding-Mxbai"</c> tenant at
/// <see cref="GpuPriorityClass.Normal"/> with <see cref="EvictionPolicy.HardHeap"/>
/// (D3D12 tier-2 demotion under VRAM pressure). All inference work is dispatched via
/// <see cref="IGpuScheduler.ScheduleAsync{T}"/> per the GPU Scheduler Contract in
/// CLAUDE.md.
/// </para>
/// <para>
/// <b>Diagnostics:</b> the locked log line
/// <c>"Embedding.Resolved: provider=onnx dim=1024 model=mxbai-embed-large-v1"</c>
/// is emitted exactly once on the first <see cref="CreateEmbeddingsAsync"/> call so
/// operators can confirm the active embedding path.
/// </para>
/// </remarks>
public sealed class MxbaiOnnxEmbeddingModel : IEmbeddingModel, IEmbeddingGeneratorBridge, IDisposable
{
    /// <summary>Locked diagnostic line (do not change wording — operators grep for it).</summary>
    internal const string ResolvedLogLine = "Embedding.Resolved: provider=onnx dim=1024 model=mxbai-embed-large-v1";

    /// <summary>GPU scheduler tenant name.</summary>
    internal const string TenantName = "Embedding-Mxbai";

    /// <summary>Hidden dimension for mxbai-embed-large-v1 (BERT-large).</summary>
    private const int HiddenDim = 1024;

    /// <summary>Estimated VRAM footprint for the FP16 model + activations (~700 MB).</summary>
    private const long EstimatedVramBytes = 700_000_000L;

    /// <summary>Maximum dispatch wall time hint for the GPU watchdog (Phase 196.5 plan 04).</summary>
    private static readonly TimeSpan MaxDispatchTime = TimeSpan.FromSeconds(5);

    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;
    private readonly IGpuScheduler? _scheduler;
    private readonly IDisposable? _tenantHandle;
    private readonly ILogger<MxbaiOnnxEmbeddingModel>? _logger;
    private readonly int _maxSequenceLength;

    private readonly string _inputIdsName;
    private readonly string _attentionMaskName;
    private readonly string? _tokenTypeIdsName;
    private readonly string _outputName;

    private readonly int _clsId;
    private readonly int _sepId;
    private readonly int _padId;

    private int _firstInferenceLogged;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MxbaiOnnxEmbeddingModel"/> class.
    /// </summary>
    /// <param name="modelPath">Absolute or relative path to the ONNX model file.</param>
    /// <param name="vocabPath">Absolute or relative path to the BERT WordPiece <c>vocab.txt</c> file.</param>
    /// <param name="sessionFactory">Shared D3D12 ORT factory (Phase 196.3). When <see langword="null"/> a CPU-only <see cref="SessionOptions"/> is built (test seam — DI never passes null in production).</param>
    /// <param name="scheduler">GPU scheduler (Phase 196.5). When <see langword="null"/> the model bypasses scheduling and runs sessions inline (test seam).</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="maxSequenceLength">Hard cap on tokens per input. Defaults to 512 (BERT-large limit).</param>
    /// <exception cref="FileNotFoundException">Thrown if either <paramref name="modelPath"/> or <paramref name="vocabPath"/> is missing.</exception>
    /// <exception cref="InvalidOperationException">Propagates from <see cref="ISharedOrtDmlSessionFactory.CreateSessionOptions"/> when the shared D3D12 device is unavailable. The DI strategy chain catches this and falls through to Ollama.</exception>
    public MxbaiOnnxEmbeddingModel(
        string modelPath,
        string vocabPath,
        ISharedOrtDmlSessionFactory? sessionFactory,
        IGpuScheduler? scheduler,
        ILogger<MxbaiOnnxEmbeddingModel>? logger,
        int maxSequenceLength = 512)
    {
        ArgumentException.ThrowIfNullOrEmpty(modelPath);
        ArgumentException.ThrowIfNullOrEmpty(vocabPath);

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"ONNX model not found: {modelPath}", modelPath);
        }

        if (!File.Exists(vocabPath))
        {
            throw new FileNotFoundException($"BERT vocab not found: {vocabPath}", vocabPath);
        }

        _logger = logger;
        _scheduler = scheduler;
        _maxSequenceLength = Math.Max(8, maxSequenceLength);

        // Tokenizer: BERT WordPiece (cache the [CLS]/[SEP]/[PAD] ids once at construction
        // per feedback_tokenizer_tensor_caching.md — no per-call lookups).
        BertOptions bertOptions = new()
        {
            LowerCaseBeforeTokenization = true,
            ApplyBasicTokenization = true,
        };
        _tokenizer = BertTokenizer.Create(vocabPath, bertOptions);
        _clsId = _tokenizer.ClassificationTokenId;
        _sepId = _tokenizer.SeparatorTokenId;
        _padId = _tokenizer.PaddingTokenId;

        // ONNX session via shared factory (per feedback_gpu_work_via_tensor.md). The
        // returned SessionOptions has the DML EP + mandatory quirks pre-applied; we
        // own disposal once the InferenceSession is constructed.
        SessionOptions options;
        if (sessionFactory is not null)
        {
            options = sessionFactory.CreateSessionOptions();
        }
        else
        {
            options = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            };
        }

        try
        {
            _session = new InferenceSession(modelPath, options);
        }
        finally
        {
            options.Dispose();
        }

        // Resolve input/output names from session metadata. mxbai exports vary on
        // whether token_type_ids is bound, so probe before assuming.
        _inputIdsName = ResolveInput(_session, "input_ids");
        _attentionMaskName = ResolveInput(_session, "attention_mask");
        _tokenTypeIdsName = _session.InputMetadata.ContainsKey("token_type_ids") ? "token_type_ids" : null;
        _outputName = _session.OutputMetadata.Keys.First();

        // Register as a Normal-priority HardHeap tenant per CLAUDE.md GPU Scheduler Contract.
        if (_scheduler is not null)
        {
            GpuTenantProfile profile = new(
                TenantName: TenantName,
                BasePriority: GpuPriorityClass.Normal,
                VramBytes: EstimatedVramBytes,
                MaxDispatchTime: MaxDispatchTime,
                Preemptible: false,
                Eviction: EvictionPolicy.HardHeap);

            _tenantHandle = _scheduler.RegisterTenant(profile);
        }
    }

    /// <inheritdoc/>
    public int Dimension => HiddenDim;

    /// <inheritdoc/>
    public async Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Locked diagnostic line — fires exactly once.
        if (Interlocked.CompareExchange(ref _firstInferenceLogged, 1, 0) == 0)
        {
            _logger?.LogInformation(ResolvedLogLine);
        }

        if (string.IsNullOrEmpty(input))
        {
            input = " ";
        }

        long[] tokenIds = TokenizeWithSpecials(input);
        int seqLen = tokenIds.Length;
        long[] attentionMask = new long[seqLen];
        Array.Fill(attentionMask, 1L);

        return await RunSessionAsync(tokenIds, attentionMask, batchCount: 1, ct).ConfigureAwait(false) is float[][] batch
            ? batch[0]
            : throw new InvalidOperationException("Session returned no output rows.");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> inputs, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (inputs.Count == 0)
        {
            return [];
        }

        if (Interlocked.CompareExchange(ref _firstInferenceLogged, 1, 0) == 0)
        {
            _logger?.LogInformation(ResolvedLogLine);
        }

        // Tokenize each input + pad to longest in the batch.
        long[][] perRowIds = new long[inputs.Count][];
        int maxLen = 0;
        for (int i = 0; i < inputs.Count; i++)
        {
            string text = string.IsNullOrEmpty(inputs[i]) ? " " : inputs[i];
            perRowIds[i] = TokenizeWithSpecials(text);
            if (perRowIds[i].Length > maxLen)
            {
                maxLen = perRowIds[i].Length;
            }
        }

        long[] paddedIds = new long[inputs.Count * maxLen];
        long[] paddedMask = new long[inputs.Count * maxLen];
        for (int i = 0; i < inputs.Count; i++)
        {
            long[] row = perRowIds[i];
            int offset = i * maxLen;
            for (int s = 0; s < row.Length; s++)
            {
                paddedIds[offset + s] = row[s];
                paddedMask[offset + s] = 1L;
            }

            for (int s = row.Length; s < maxLen; s++)
            {
                paddedIds[offset + s] = _padId;
                paddedMask[offset + s] = 0L;
            }
        }

        return await RunSessionAsync(paddedIds, paddedMask, batchCount: inputs.Count, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Tokenizes <paramref name="text"/> with [CLS] + WordPiece tokens + [SEP], hard-truncated
    /// to <see cref="_maxSequenceLength"/>. The [SEP] terminator is always preserved at the
    /// last position even when truncating.
    /// </summary>
    /// <param name="text">Input string.</param>
    /// <returns>Token id array including special tokens.</returns>
    internal long[] TokenizeWithSpecials(string text)
    {
        IReadOnlyList<int> rawIds = _tokenizer.EncodeToIds(text, addSpecialTokens: false, considerPreTokenization: true, considerNormalization: true);

        // Reserve room for [CLS] + [SEP].
        int budget = _maxSequenceLength - 2;
        int take = Math.Min(rawIds.Count, budget);
        long[] tokens = new long[take + 2];
        tokens[0] = _clsId;
        for (int i = 0; i < take; i++)
        {
            tokens[i + 1] = rawIds[i];
        }

        tokens[take + 1] = _sepId;
        return tokens;
    }

    /// <summary>
    /// Runs the ONNX session for a flat <c>[B,S]</c> tensor and returns one pooled,
    /// L2-normalized vector per row. Dispatched through the GPU scheduler when one
    /// is registered (per Phase 196.5 contract).
    /// </summary>
    /// <param name="ids">Flattened input ids of length <c>batchCount * seqLen</c>.</param>
    /// <param name="mask">Flattened attention mask of length <c>batchCount * seqLen</c>.</param>
    /// <param name="batchCount">Batch dimension.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One float vector per batch row.</returns>
    private Task<float[][]> RunSessionAsync(long[] ids, long[] mask, int batchCount, CancellationToken ct)
    {
        if (_scheduler is not null)
        {
            return _scheduler.ScheduleAsync(
                TenantName,
                token => Task.FromResult(RunSession(ids, mask, batchCount)),
                ct);
        }

        return Task.FromResult(RunSession(ids, mask, batchCount));
    }

    /// <summary>
    /// Synchronous ONNX session invocation. Builds the named-tensor inputs, runs the
    /// session, masked-mean-pools each row, and L2-normalizes.
    /// </summary>
    /// <param name="ids">Flattened input ids.</param>
    /// <param name="mask">Flattened attention mask.</param>
    /// <param name="batchCount">Batch dimension.</param>
    /// <returns>Pooled + normalized vectors, one per batch row.</returns>
    private float[][] RunSession(long[] ids, long[] mask, int batchCount)
    {
        int seqLen = ids.Length / batchCount;
        DenseTensor<long> idsTensor = new(ids, [batchCount, seqLen]);
        DenseTensor<long> maskTensor = new(mask, [batchCount, seqLen]);

        List<NamedOnnxValue> inputs = new(3)
        {
            NamedOnnxValue.CreateFromTensor(_inputIdsName, idsTensor),
            NamedOnnxValue.CreateFromTensor(_attentionMaskName, maskTensor),
        };

        DenseTensor<long>? typeTensor = null;
        if (_tokenTypeIdsName is not null)
        {
            long[] zeros = new long[ids.Length]; // single-segment, all zeros
            typeTensor = new DenseTensor<long>(zeros, [batchCount, seqLen]);
            inputs.Add(NamedOnnxValue.CreateFromTensor(_tokenTypeIdsName, typeTensor));
        }

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);
        DisposableNamedOnnxValue first = results.First(r => string.Equals(r.Name, _outputName, StringComparison.Ordinal));
        float[] flat = first.AsEnumerable<float>().ToArray();

        // Expected shape [B, S, H]. Slice per row + pool.
        float[][] outputs = new float[batchCount][];
        int rowStride = seqLen * HiddenDim;
        for (int b = 0; b < batchCount; b++)
        {
            ReadOnlySpan<float> rowHidden = flat.AsSpan(b * rowStride, rowStride);
            ReadOnlySpan<long> rowMask = mask.AsSpan(b * seqLen, seqLen);
            float[] pooled = ApplyMaskedMeanPool(rowHidden, rowMask, HiddenDim);
            ApplyL2Normalize(pooled);
            outputs[b] = pooled;
        }

        return outputs;
    }

    /// <summary>
    /// Applies attention-mask-weighted mean pooling over the sequence dimension.
    /// Sums token vectors weighted by mask (1 = include, 0 = exclude) then divides
    /// by the sum of the mask (NOT the raw sequence length — this is the bug
    /// <see cref="TensorEmbeddingModel"/>'s ONNX branch had on padded sequences).
    /// </summary>
    /// <param name="hiddenStates">Flattened <c>[S, H]</c> hidden states for one row.</param>
    /// <param name="attentionMask">Mask of length <c>S</c>.</param>
    /// <param name="hiddenDim">Hidden dimension <c>H</c>.</param>
    /// <returns>Pooled vector of length <paramref name="hiddenDim"/>.</returns>
    internal static float[] ApplyMaskedMeanPool(ReadOnlySpan<float> hiddenStates, ReadOnlySpan<long> attentionMask, int hiddenDim)
    {
        int seqLen = attentionMask.Length;
        if (hiddenStates.Length != seqLen * hiddenDim)
        {
            throw new ArgumentException($"hiddenStates length {hiddenStates.Length} does not match seqLen*hiddenDim ({seqLen}*{hiddenDim})", nameof(hiddenStates));
        }

        float[] pooled = new float[hiddenDim];
        long maskSum = 0;
        for (int s = 0; s < seqLen; s++)
        {
            long m = attentionMask[s];
            if (m == 0)
            {
                continue;
            }

            maskSum += m;
            ReadOnlySpan<float> tokenVec = hiddenStates.Slice(s * hiddenDim, hiddenDim);
            for (int h = 0; h < hiddenDim; h++)
            {
                pooled[h] += tokenVec[h];
            }
        }

        if (maskSum > 0)
        {
            float divisor = maskSum;
            TensorPrimitives.Divide(pooled, divisor, pooled);
        }

        return pooled;
    }

    /// <summary>
    /// L2-normalizes <paramref name="vector"/> in place. No-op if the norm is below
    /// 1e-10 (avoids divide-by-zero on degenerate inputs).
    /// </summary>
    /// <param name="vector">Vector to normalize.</param>
    internal static void ApplyL2Normalize(Span<float> vector)
    {
        float norm = TensorPrimitives.Norm(vector);
        if (norm > 1e-10f)
        {
            TensorPrimitives.Divide(vector, norm, vector);
        }
    }

    /// <inheritdoc/>
    public IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator()
        => new EmbeddingModelGeneratorAdapter(this);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _tenantHandle?.Dispose();
        _session.Dispose();
    }

    private static string ResolveInput(InferenceSession session, string preferredName)
    {
        if (session.InputMetadata.ContainsKey(preferredName))
        {
            return preferredName;
        }

        // Fall back to the first input — keeps us resilient to exporters that rename.
        return session.InputMetadata.Keys.First();
    }
}
