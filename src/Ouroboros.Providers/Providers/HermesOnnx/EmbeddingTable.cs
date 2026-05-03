// <copyright file="EmbeddingTable.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ouroboros.Abstractions.Embeddings;

namespace Ouroboros.Providers.HermesOnnx;

/// <summary>
/// Phase 264 runtime-mutable embedding table. Loads the FP16 token-embedding
/// baseline from <c>embedding_table.fp16.bin</c> (extracted from the source
/// HF weights at build time, sibling to the noembed model.onnx), maintains a
/// working copy that can drift via <see cref="IEmbeddingMutator"/>, and
/// produces a contiguous <c>[seq_len, hidden]</c> FP16 buffer per inference
/// call for feeding as <c>inputs_embeds</c> via
/// <c>Generator.SetModelInput</c>.
/// </summary>
/// <remarks>
/// <para>
/// The baseline is loaded once and frozen. <see cref="IEmbeddingMutator.SetRow"/>
/// / <see cref="IEmbeddingMutator.AddDelta"/> mutate the working copy only —
/// <see cref="IEmbeddingMutator.ResetRow"/> snaps a single row back to baseline
/// without rebuilding the whole table.
/// </para>
/// <para>
/// Vector dtype is <see cref="Half"/> (FP16) to match what the model expects
/// at the <c>inputs_embeds</c> input. Storage is a single flat
/// <c>Half[vocab × hidden]</c> per copy (baseline + working) — for
/// Hermes-3-Llama-3.1-8B that's 128256 × 4096 × 2 bytes = ~1 GB per copy.
/// Held in pinned host memory; per-call lookup copies only the seq_len rows
/// the inference actually needs.
/// </para>
/// <para>
/// Thread safety: mutations are gated by <see cref="_mutationLock"/>; reads
/// (Lookup) take a read lock. Concurrent inferences against the same table
/// are safe; concurrent mutation + inference will serialize.
/// </para>
/// </remarks>
public sealed class EmbeddingTable : IEmbeddingMutator, IDisposable
{
    private readonly Half[] _baseline;       // frozen, never written after load
    private readonly Half[] _working;        // mutable copy
    private readonly HashSet<int> _diverged; // tokens whose working differs from baseline
    private readonly object _mutationLock = new();
    private readonly ILogger? _logger;
    private Half[] _softPromptPrefix = [];
    private bool _disposed;

    /// <inheritdoc/>
    public int VocabSize { get; }

    /// <inheritdoc/>
    public int HiddenSize { get; }

    /// <summary>
    /// Initializes the embedding table by mmaping the baseline binary and
    /// allocating the working copy. <paramref name="modelPath"/> is the
    /// directory containing <c>model.onnx</c> + <c>embedding_table.fp16.bin</c>.
    /// </summary>
    /// <exception cref="FileNotFoundException">
    /// Thrown when <c>embedding_table.fp16.bin</c> or its sidecar JSON is missing.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when the binary size doesn't match the JSON-declared shape.
    /// </exception>
    public EmbeddingTable(string modelPath, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        _logger = logger;

        string binPath = Path.Combine(modelPath, "embedding_table.fp16.bin");
        string jsonPath = Path.Combine(modelPath, "embedding_table.fp16.json");

        if (!File.Exists(binPath))
        {
            throw new FileNotFoundException(
                $"Embedding table binary not found at {binPath}. " +
                "Phase 264 (runtime-mutable embeddings) requires a noembed-built model. " +
                "See docs/hermes-onnx-rebuild.md for the rebuild recipe.",
                binPath);
        }

        if (!File.Exists(jsonPath))
        {
            throw new FileNotFoundException(
                $"Embedding table sidecar JSON not found at {jsonPath}. " +
                "The .bin without its sidecar can't be validated for vocab/hidden shape.",
                jsonPath);
        }

        // Parse the sidecar JSON for shape + dtype validation.
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        VocabSize = doc.RootElement.GetProperty("vocab_size").GetInt32();
        HiddenSize = doc.RootElement.GetProperty("hidden_size").GetInt32();
        string dtype = doc.RootElement.GetProperty("dtype").GetString() ?? "";
        if (dtype != "float16")
        {
            throw new InvalidDataException(
                $"Embedding table dtype '{dtype}' unsupported; only float16 implemented in v1.");
        }

        long expectedBytes = (long)VocabSize * HiddenSize * 2;
        long actualBytes = new FileInfo(binPath).Length;
        if (actualBytes != expectedBytes)
        {
            throw new InvalidDataException(
                $"Embedding table size mismatch: expected {expectedBytes:N0} bytes " +
                $"({VocabSize}×{HiddenSize}×2), got {actualBytes:N0} bytes at {binPath}.");
        }

        _logger?.LogInformation(
            "[EmbeddingTable] loading baseline {Vocab}×{Hidden} fp16 ({Bytes:N0} bytes) from {Path}",
            VocabSize, HiddenSize, expectedBytes, binPath);

        _baseline = new Half[VocabSize * HiddenSize];
        _working = new Half[VocabSize * HiddenSize];
        _diverged = new HashSet<int>(capacity: 64);

        // Read raw fp16 bytes into Half[] storage. Doing this once at startup
        // is acceptable — a 1 GB sequential read off NVMe is ~3 seconds.
        using FileStream fs = File.OpenRead(binPath);
        Span<byte> buf = stackalloc byte[2];
        for (int i = 0; i < _baseline.Length; i++)
        {
            int read = fs.Read(buf);
            if (read != 2)
            {
                throw new EndOfStreamException(
                    $"Embedding table truncated at element {i}/{_baseline.Length}");
            }

            ushort bits = BinaryPrimitives.ReadUInt16LittleEndian(buf);
            _baseline[i] = BitConverter.UInt16BitsToHalf(bits);
        }

        // Working copy starts at baseline parity.
        Buffer.BlockCopy(_baseline, 0, _working, 0, _baseline.Length * 2);

        _logger?.LogInformation("[EmbeddingTable] baseline + working copies ready");
    }

    /// <summary>
    /// Looks up the embedding rows for <paramref name="tokenIds"/> and writes
    /// them contiguously into <paramref name="output"/>. Output layout is
    /// row-major <c>[tokenIds.Length, HiddenSize]</c> for direct feeding as
    /// <c>inputs_embeds</c>.
    /// </summary>
    /// <remarks>
    /// If a soft-prompt prefix is configured via <see cref="SetSoftPromptPrefix"/>,
    /// it is prepended to the output. Caller is responsible for sizing
    /// <paramref name="output"/> = <c>(tokenIds.Length + softPromptLen/HiddenSize) × HiddenSize</c>.
    /// </remarks>
    public void Lookup(ReadOnlySpan<int> tokenIds, Span<Half> output)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_mutationLock)
        {
            int outOffset = 0;

            // Soft-prompt prefix first (if any).
            ReadOnlySpan<Half> prefix = _softPromptPrefix;
            if (prefix.Length > 0)
            {
                if (output.Length < prefix.Length + tokenIds.Length * HiddenSize)
                {
                    throw new ArgumentException(
                        $"Output buffer too small: need {prefix.Length + tokenIds.Length * HiddenSize}, got {output.Length}",
                        nameof(output));
                }

                prefix.CopyTo(output);
                outOffset += prefix.Length;
            }

            // Per-token row lookup against the working copy.
            for (int i = 0; i < tokenIds.Length; i++)
            {
                int tokenId = tokenIds[i];
                if ((uint)tokenId >= (uint)VocabSize)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(tokenIds), tokenId,
                        $"Token id out of vocab range [0, {VocabSize}).");
                }

                int srcOffset = tokenId * HiddenSize;
                ReadOnlySpan<Half> row = _working.AsSpan(srcOffset, HiddenSize);
                row.CopyTo(output.Slice(outOffset, HiddenSize));
                outOffset += HiddenSize;
            }
        }
    }

    /// <summary>
    /// Returns the current soft-prompt prefix length in elements
    /// (HiddenSize × prefix-token-count). Useful for sizing the output buffer
    /// before <see cref="Lookup"/>.
    /// </summary>
    public int SoftPromptLength => _softPromptPrefix.Length;

    /// <inheritdoc/>
    public void SetRow(int tokenId, ReadOnlySpan<Half> vector)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateTokenId(tokenId);
        if (vector.Length != HiddenSize)
        {
            throw new ArgumentException(
                $"Vector length {vector.Length} must equal HiddenSize {HiddenSize}",
                nameof(vector));
        }

        lock (_mutationLock)
        {
            vector.CopyTo(_working.AsSpan(tokenId * HiddenSize, HiddenSize));
            _diverged.Add(tokenId);
        }
    }

    /// <inheritdoc/>
    public void AddDelta(int tokenId, ReadOnlySpan<Half> delta, float scale = 1.0f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateTokenId(tokenId);
        if (delta.Length != HiddenSize)
        {
            throw new ArgumentException(
                $"Delta length {delta.Length} must equal HiddenSize {HiddenSize}",
                nameof(delta));
        }

        lock (_mutationLock)
        {
            Span<Half> row = _working.AsSpan(tokenId * HiddenSize, HiddenSize);
            for (int i = 0; i < HiddenSize; i++)
            {
                float current = (float)row[i];
                float deltaF = (float)delta[i];
                row[i] = (Half)(current + deltaF * scale);
            }

            _diverged.Add(tokenId);
        }
    }

    /// <inheritdoc/>
    public void ResetRow(int tokenId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateTokenId(tokenId);

        lock (_mutationLock)
        {
            ReadOnlySpan<Half> baseline = _baseline.AsSpan(tokenId * HiddenSize, HiddenSize);
            baseline.CopyTo(_working.AsSpan(tokenId * HiddenSize, HiddenSize));
            _diverged.Remove(tokenId);
        }
    }

    /// <inheritdoc/>
    public void SetSoftPromptPrefix(ReadOnlyMemory<Half> prefixEmbeds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (prefixEmbeds.Length % HiddenSize != 0)
        {
            throw new ArgumentException(
                $"Soft-prompt prefix length {prefixEmbeds.Length} must be a multiple of HiddenSize {HiddenSize}",
                nameof(prefixEmbeds));
        }

        lock (_mutationLock)
        {
            _softPromptPrefix = prefixEmbeds.ToArray();
        }
    }

    /// <inheritdoc/>
    public EmbeddingSnapshot Snapshot()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_mutationLock)
        {
            Dictionary<int, ReadOnlyMemory<Half>> rows = new(capacity: _diverged.Count);
            foreach (int tokenId in _diverged)
            {
                Half[] copy = new Half[HiddenSize];
                _working.AsSpan(tokenId * HiddenSize, HiddenSize).CopyTo(copy);
                rows[tokenId] = copy;
            }

            return new EmbeddingSnapshot
            {
                MutatedRows = rows,
                SoftPromptPrefix = (Half[])_softPromptPrefix.Clone(),
                VocabSize = VocabSize,
                HiddenSize = HiddenSize,
                CapturedAt = DateTimeOffset.UtcNow,
            };
        }
    }

    /// <inheritdoc/>
    public void Restore(EmbeddingSnapshot snapshot)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.VocabSize != VocabSize || snapshot.HiddenSize != HiddenSize)
        {
            throw new ArgumentException(
                $"Snapshot shape ({snapshot.VocabSize}, {snapshot.HiddenSize}) doesn't match table ({VocabSize}, {HiddenSize})",
                nameof(snapshot));
        }

        lock (_mutationLock)
        {
            // First snap working back to baseline.
            Buffer.BlockCopy(_baseline, 0, _working, 0, _baseline.Length * 2);
            _diverged.Clear();

            // Then replay the snapshot's mutated rows.
            foreach (var kv in snapshot.MutatedRows)
            {
                int tokenId = kv.Key;
                if ((uint)tokenId >= (uint)VocabSize) continue;
                if (kv.Value.Length != HiddenSize) continue;

                kv.Value.Span.CopyTo(_working.AsSpan(tokenId * HiddenSize, HiddenSize));
                _diverged.Add(tokenId);
            }

            _softPromptPrefix = snapshot.SoftPromptPrefix.ToArray();
        }
    }

    /// <inheritdoc/>
    public void ResetAll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_mutationLock)
        {
            Buffer.BlockCopy(_baseline, 0, _working, 0, _baseline.Length * 2);
            _diverged.Clear();
            _softPromptPrefix = [];
        }
    }

    /// <summary>Number of token rows that diverge from baseline right now.</summary>
    public int DivergedRowCount
    {
        get
        {
            lock (_mutationLock)
            {
                return _diverged.Count;
            }
        }
    }

    private void ValidateTokenId(int tokenId)
    {
        if ((uint)tokenId >= (uint)VocabSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tokenId), tokenId,
                $"Token id out of vocab range [0, {VocabSize}).");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        // Half[] arrays are managed; nothing native to release. The 1 GB
        // baseline + 1 GB working will be GC'd on next collection.
    }
}
