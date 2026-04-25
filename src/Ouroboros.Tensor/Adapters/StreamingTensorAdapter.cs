// <copyright file="StreamingTensorAdapter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Converts a streaming sequence of float arrays into batched tensors via
/// <see cref="IAsyncEnumerable{T}"/> (R10).
/// </summary>
/// <remarks>
/// <para>
/// Vectors are accumulated into a staging buffer and emitted as a 2-D tensor of shape
/// <c>[batchSize, dimension]</c> when the buffer is full. A partial final batch is always
/// emitted even if fewer than <c>batchSize</c> vectors were received.
/// </para>
/// <para>
/// A single temporary <see cref="ArrayPool{T}"/> rental is used for staging each batch;
/// the rental is returned immediately after the tensor is created (R12).
/// </para>
/// </remarks>
public static class StreamingTensorAdapter
{
    /// <summary>
    /// Adapts a streaming sequence of float arrays into an async sequence of batched tensors.
    /// </summary>
    /// <param name="source">Source sequence of float arrays (e.g. decoded embeddings).</param>
    /// <param name="backend">Backend used to construct each batch tensor.</param>
    /// <param name="batchSize">
    /// Number of vectors per emitted tensor. The final tensor may contain fewer elements.
    /// </param>
    /// <param name="cancellationToken">Cancellation token threaded through the async iteration.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of <see cref="ITensor{T}"/> instances, each with
    /// shape <c>[n, dim]</c> where <c>n ≤ batchSize</c>. Callers must dispose each tensor.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="batchSize"/> is not positive.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a vector's dimension is inconsistent with the dimension of the first vector
    /// in the stream.
    /// </exception>
    public static IAsyncEnumerable<ITensor<float>> AdaptAsync(
        IAsyncEnumerable<float[]> source,
        ITensorBackend backend,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(backend);
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive.");
        }

        return AdaptAsyncCore(source, backend, batchSize, cancellationToken);
    }

    private static async IAsyncEnumerable<ITensor<float>> AdaptAsyncCore(
        IAsyncEnumerable<float[]> source,
        ITensorBackend backend,
        int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var batch = new List<float[]>(batchSize);
        int? expectedDim = null;

        await foreach (var vector in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (vector.Length == 0)
            {
                continue;
            }

            expectedDim ??= vector.Length;

            if (vector.Length != expectedDim.Value)
            {
                throw new ArgumentException(
                    $"Inconsistent vector dimension at stream position {batch.Count + 1}: " +
                    $"expected {expectedDim.Value}, got {vector.Length}.");
            }

            batch.Add(vector);

            if (batch.Count == batchSize)
            {
                yield return CreateBatchTensor(batch, expectedDim.Value, backend);
                batch.Clear();
            }
        }

        // Emit partial final batch
        if (batch.Count > 0)
        {
            yield return CreateBatchTensor(batch, expectedDim!.Value, backend);
        }
    }

    private static ITensor<float> CreateBatchTensor(
        List<float[]> batch, int dim, ITensorBackend backend)
    {
        var shape = TensorShape.Of(batch.Count, dim);
        var totalElements = batch.Count * dim;
        var buffer = ArrayPool<float>.Shared.Rent(totalElements);
        try
        {
            for (var i = 0; i < batch.Count; i++)
            {
                batch[i].AsSpan().CopyTo(buffer.AsSpan(i * dim, dim));
            }

            return backend.Create(shape, buffer.AsSpan(0, totalElements));
        }
        finally
        {
            // Rental returned immediately; the tensor owns its own pooled copy
            ArrayPool<float>.Shared.Return(buffer);
        }
    }
}
