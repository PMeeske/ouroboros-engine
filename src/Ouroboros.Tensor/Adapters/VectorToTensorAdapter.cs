// <copyright file="VectorToTensorAdapter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Converts raw float arrays (vector embeddings) into <see cref="ITensor{T}"/> instances
/// using the configured backend. Minimises copies where the backend supports zero-copy
/// wrapping of existing memory (R09).
/// </summary>
public sealed class VectorToTensorAdapter
{
    private readonly ITensorBackend _backend;

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorToTensorAdapter"/> class.
    /// Initializes a new <see cref="VectorToTensorAdapter"/> using the given backend.
    /// </summary>
    public VectorToTensorAdapter(ITensorBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        _backend = backend;
    }

    /// <summary>
    /// Converts a single flat float array into a 1-D tensor of shape <c>[vector.Length]</c>.
    /// Uses <see cref="ITensorBackend.FromMemory"/> to avoid copying when possible.
    /// </summary>
    /// <param name="vector">The source vector data.</param>
    /// <returns>A tensor wrapping the vector data; caller is responsible for disposal.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="vector"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="vector"/> is empty.</exception>
    public ITensor<float> Convert(float[] vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        if (vector.Length == 0)
        {
            throw new ArgumentException("Vector must not be empty.", nameof(vector));
        }

        var shape = TensorShape.Of(vector.Length);

        // Zero-copy path: wrap existing managed array as ReadOnlyMemory
        return _backend.FromMemory(vector.AsMemory(), shape);
    }

    /// <summary>
    /// Converts a batch of float arrays into a 2-D tensor of shape <c>[count, dimension]</c>.
    /// All vectors in the batch must have the same dimension.
    /// A single copy into a pooled buffer is performed.
    /// </summary>
    /// <param name="vectors">The batch of vectors. Must be non-empty and uniform in length.</param>
    /// <returns>A 2-D tensor; caller is responsible for disposal.</returns>
    /// <exception cref="ArgumentException">Thrown when the batch is empty or dimensions are inconsistent.</exception>
    public ITensor<float> ConvertBatch(IReadOnlyList<float[]> vectors)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        if (vectors.Count == 0)
        {
            throw new ArgumentException("Batch must not be empty.", nameof(vectors));
        }

        var dim = vectors[0].Length;
        foreach (var v in vectors)
        {
            if (v.Length != dim)
            {
                throw new ArgumentException(
                    $"All vectors in a batch must have the same dimension. " +
                    $"Expected {dim}, got {v.Length}.", nameof(vectors));
            }
        }

        var shape = TensorShape.Of(vectors.Count, dim);
        var buffer = ArrayPool<float>.Shared.Rent(vectors.Count * dim);
        try
        {
            for (var i = 0; i < vectors.Count; i++)
            {
                vectors[i].AsSpan().CopyTo(buffer.AsSpan(i * dim, dim));
            }

            // Create copies buffer content into pooled tensor storage
            return _backend.Create(shape, buffer.AsSpan(0, vectors.Count * dim));
        }
        finally
        {
            ArrayPool<float>.Shared.Return(buffer);
        }
    }
}
