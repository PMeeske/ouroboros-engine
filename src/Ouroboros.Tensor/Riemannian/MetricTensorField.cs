// <copyright file="MetricTensorField.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Riemannian;

/// <summary>
/// A learned positive-definite metric tensor field <c>g_x</c> over a finite
/// number of dimensions.
/// </summary>
/// <remarks>
/// <para>
/// The default metric is the identity (Euclidean). Calling
/// <see cref="Update"/> with new diagonal scaling factors learns a
/// per-dimension metric: <c>g_x = diag(scales) + alpha * I</c> for
/// stability. The <see cref="ComputeMetricTensor"/> output is symmetric and
/// positive definite by construction.
/// </para>
/// <para>
/// <see cref="ComputeDistance"/> implements the Mahalanobis-style distance
/// <c>d(a,b) = sqrt((a-b)^T g (a-b))</c> which is the appropriate edge
/// weight for the <see cref="DiscreteGeodesicReasoner"/>.
/// </para>
/// </remarks>
public sealed class MetricTensorField
{
    private const float StabilityFloor = 1e-3f;

    private readonly int _dimensions;
    private float[] _diagonal;

    /// <summary>
    /// Initializes a new instance with the identity metric.
    /// </summary>
    /// <param name="dimensions">Embedding dimension count (must be &gt;= 1).</param>
    public MetricTensorField(int dimensions)
    {
        if (dimensions < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions), "Dimensions must be >= 1.");
        }

        _dimensions = dimensions;
        _diagonal = new float[dimensions];
        for (int i = 0; i < dimensions; i++)
        {
            _diagonal[i] = 1f;
        }
    }

    /// <summary>
    /// Gets the dimension count.
    /// </summary>
    public int Dimensions => _dimensions;

    /// <summary>
    /// Updates the diagonal scaling factors. Negative or NaN inputs are
    /// floored at <see cref="StabilityFloor"/> to preserve positive
    /// definiteness.
    /// </summary>
    /// <param name="diagonalScales">New diagonal scales (length must equal <see cref="Dimensions"/>).</param>
    public void Update(ReadOnlySpan<float> diagonalScales)
    {
        if (diagonalScales.Length != _dimensions)
        {
            throw new ArgumentException(
                $"Expected {_dimensions} scales, got {diagonalScales.Length}.",
                nameof(diagonalScales));
        }

        float[] newDiagonal = new float[_dimensions];
        for (int i = 0; i < _dimensions; i++)
        {
            float v = diagonalScales[i];
            newDiagonal[i] = float.IsNaN(v) || v < StabilityFloor ? StabilityFloor : v;
        }

        _diagonal = newDiagonal;
    }

    /// <summary>
    /// Computes the metric tensor at a point. The current implementation is
    /// position-independent (<c>g_x = g</c>), but exposes the parameter so a
    /// future learned-field implementation can specialize per location.
    /// </summary>
    /// <param name="point">The query point (currently unused; reserved).</param>
    /// <returns>A new symmetric positive-definite matrix.</returns>
    public float[,] ComputeMetricTensor(ReadOnlySpan<float> point)
    {
        _ = point; // Reserved for position-dependent metrics.

        float[,] g = new float[_dimensions, _dimensions];
        for (int i = 0; i < _dimensions; i++)
        {
            g[i, i] = _diagonal[i];
        }

        return g;
    }

    /// <summary>
    /// Tests whether a matrix is symmetric and positive definite. Uses
    /// Cholesky factorization with a small numerical tolerance.
    /// </summary>
    /// <param name="g">The candidate matrix.</param>
    /// <returns>True iff symmetric and positive definite.</returns>
    public static bool IsPositiveDefinite(float[,] g)
    {
        ArgumentNullException.ThrowIfNull(g);

        int n = g.GetLength(0);
        if (n == 0 || g.GetLength(1) != n)
        {
            return false;
        }

        // Symmetry check.
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                float diff = g[i, j] - g[j, i];
                if (Math.Abs(diff) > 1e-5f)
                {
                    return false;
                }
            }
        }

        // Cholesky: succeed iff all leading principal minors are positive.
        double[,] l = new double[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                double sum = g[i, j];
                for (int k = 0; k < j; k++)
                {
                    sum -= l[i, k] * l[j, k];
                }

                if (i == j)
                {
                    if (sum <= 0.0)
                    {
                        return false;
                    }

                    l[i, j] = Math.Sqrt(sum);
                }
                else
                {
                    l[i, j] = sum / l[j, j];
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Computes the Mahalanobis distance <c>sqrt((a-b)^T g (a-b))</c>.
    /// </summary>
    /// <param name="a">First point.</param>
    /// <param name="b">Second point.</param>
    /// <returns>Non-negative distance.</returns>
    public float ComputeDistance(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != _dimensions || b.Length != _dimensions)
        {
            throw new ArgumentException(
                $"Both vectors must have length {_dimensions}.");
        }

        double accumulator = 0.0;
        for (int i = 0; i < _dimensions; i++)
        {
            double diff = a[i] - b[i];
            accumulator += _diagonal[i] * diff * diff;
        }

        return (float)Math.Sqrt(Math.Max(accumulator, 0.0));
    }
}
