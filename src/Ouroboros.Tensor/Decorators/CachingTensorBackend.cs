// <copyright file="CachingTensorBackend.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Decorators;

/// <summary>
/// Decorator that memoises <see cref="ITensorBackend.Create"/> calls with identical shapes and
/// data content, avoiding redundant compute on repeated identical inputs (R11).
/// </summary>
/// <remarks>
/// <para>
/// Cache keys are derived from the shape and the FNV-1a hash of the float data. This is a
/// best-effort cache: hash collisions are theoretically possible but practically negligible
/// for the use case of caching large numeric tensors.
/// </para>
/// <para>
/// The cache is bounded by <see cref="MaxEntries"/>. Entries beyond the limit cause the
/// oldest entry to be evicted (LRU-lite using insertion order of a <see cref="ConcurrentDictionary"/>
/// with a separate tracking list). For simplicity, <see cref="MatMul"/> and <see cref="Add"/>
/// results are <em>not</em> cached because their identity depends on mutable operand state.
/// </para>
/// <para>
/// Cached tensors are <em>copies</em>: the caching layer owns its pooled buffers independently
/// of the caller. Disposing a cached tensor marks it invalid; subsequent hits will re-compute.
/// </para>
/// </remarks>
public sealed class CachingTensorBackend : ITensorBackend
{
    /// <summary>Maximum number of tensors held in cache.</summary>
    public const int MaxEntries = 256;

    private readonly ITensorBackend _inner;
    private readonly ConcurrentDictionary<CacheKey, ITensor<float>> _cache = new();
    private readonly Queue<CacheKey> _insertionOrder = new();
    private readonly object _evictionLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CachingTensorBackend"/> class.
    /// Initializes a new <see cref="CachingTensorBackend"/> wrapping <paramref name="inner"/>.
    /// </summary>
    public CachingTensorBackend(ITensorBackend inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <inheritdoc/>
    public DeviceType Device => _inner.Device;

    /// <inheritdoc/>
    /// <remarks>
    /// Returns a cached tensor when the shape and content hash match a previous call.
    /// The returned tensor must still be disposed by the caller.
    /// </remarks>
    public ITensor<float> Create(TensorShape shape, ReadOnlySpan<float> data)
    {
        var key = CacheKey.From(shape, data);

        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var tensor = _inner.Create(shape, data);
        AddToCache(key, tensor);
        return tensor;
    }

    /// <inheritdoc/>
    public ITensor<float> CreateUninitialized(TensorShape shape)
        => _inner.CreateUninitialized(shape); // Never cached: contents are undefined

    /// <inheritdoc/>
    public ITensor<float> FromMemory(ReadOnlyMemory<float> memory, TensorShape shape)
        => _inner.FromMemory(memory, shape); // Zero-copy path; no cache needed

    /// <inheritdoc/>
    public Result<ITensor<float>, string> MatMul(ITensor<float> a, ITensor<float> b)
        => _inner.MatMul(a, b);

    /// <inheritdoc/>
    public Result<ITensor<float>, string> Add(ITensor<float> a, ITensor<float> b)
        => _inner.Add(a, b);

    private void AddToCache(CacheKey key, ITensor<float> tensor)
    {
        lock (_evictionLock)
        {
            if (_cache.Count >= MaxEntries && _insertionOrder.TryDequeue(out var oldest))
            {
                _cache.TryRemove(oldest, out _);
            }

            if (_cache.TryAdd(key, tensor))
            {
                _insertionOrder.Enqueue(key);
            }
        }
    }

    private readonly record struct CacheKey(TensorShape Shape, uint Hash)
    {
        public static CacheKey From(TensorShape shape, ReadOnlySpan<float> data)
            => new(shape, Fnv1aHash(data));

        private static uint Fnv1aHash(ReadOnlySpan<float> data)
        {
            const uint FnvPrime = 16777619u;
            const uint FnvOffset = 2166136261u;

            var hash = FnvOffset;
            var bytes = MemoryMarshal.AsBytes(data);
            foreach (var b in bytes)
            {
                hash = (hash ^ b) * FnvPrime;
            }

            return hash;
        }
    }
}
