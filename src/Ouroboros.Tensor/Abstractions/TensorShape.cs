// <copyright file="TensorShape.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Abstractions;

/// <summary>
/// Describes the rank and extent of each dimension of a tensor.
/// </summary>
/// <remarks>
/// Uses <see cref="ImmutableArray{T}"/> to guarantee structural equality semantics
/// when used inside records or as dictionary keys.
/// </remarks>
public readonly record struct TensorShape
{
    /// <summary>
    /// Gets the per-dimension sizes.
    /// </summary>
    public ImmutableArray<int> Dimensions { get; }

    /// <summary>Initializes a new <see cref="TensorShape"/> from an immutable dimension array.</summary>
    public TensorShape(ImmutableArray<int> dimensions)
    {
        foreach (var d in dimensions)
        {
            if (d <= 0)
                throw new ArgumentOutOfRangeException(nameof(dimensions),
                    $"All dimensions must be positive. Got {d}.");
        }

        Dimensions = dimensions;
    }

    /// <summary>
    /// Creates a <see cref="TensorShape"/> from a params array of dimension sizes.
    /// </summary>
    /// <example><code>TensorShape.Of(2, 3) // 2×3 matrix</code></example>
    public static TensorShape Of(params int[] dimensions)
        => new(ImmutableArray.Create(dimensions));

    /// <summary>Gets the number of dimensions (rank).</summary>
    public int Rank => Dimensions.Length;

    /// <summary>Gets the total number of elements (product of all dimensions).</summary>
    public long ElementCount => Dimensions.IsEmpty ? 0L : Dimensions.Aggregate(1L, (acc, d) => acc * d);

    /// <summary>
    /// Returns <see langword="true"/> if this shape has the same dimensions as <paramref name="other"/>.
    /// </summary>
    public bool IsCompatibleWith(TensorShape other) => Dimensions.SequenceEqual(other.Dimensions);

    /// <inheritdoc/>
    public bool Equals(TensorShape other)
        => Dimensions.AsSpan().SequenceEqual(other.Dimensions.AsSpan());

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var d in Dimensions) hash.Add(d);
        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public override string ToString()
        => Dimensions.IsEmpty ? "[]" : $"[{string.Join(", ", Dimensions)}]";
}
