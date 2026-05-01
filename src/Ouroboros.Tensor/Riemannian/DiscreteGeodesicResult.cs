// <copyright file="DiscreteGeodesicResult.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Riemannian;

/// <summary>
/// Concrete shortest-path result returned by the
/// <see cref="DiscreteGeodesicReasoner"/>.
/// </summary>
/// <remarks>
/// This is the engine-internal counterpart to
/// <c>Ouroboros.Manifold.SemanticGeodesicResult</c>: the manifold record
/// is the public-facing pipeline output; this record carries graph-level
/// detail (node ids, per-edge costs) for diagnostics.
/// </remarks>
/// <param name="Source">Path source node.</param>
/// <param name="Target">Path target node.</param>
/// <param name="PathNodes">Ordered nodes from source to target (inclusive).</param>
/// <param name="SegmentCosts">Per-edge weights along the path (length = PathNodes.Count - 1).</param>
/// <param name="ComputedPathCost">Total path cost — sum of <paramref name="SegmentCosts"/>.</param>
/// <param name="CurvatureProxy">A 0..1 stand-in for path curvature (variance of segment costs).</param>
/// <param name="IsValid">True iff a finite path was found AND <paramref name="PathNodes"/> reflects it.</param>
public sealed record DiscreteGeodesicResult(
    NodeId Source,
    NodeId Target,
    IReadOnlyList<NodeId> PathNodes,
    IReadOnlyList<float> SegmentCosts,
    float ComputedPathCost,
    float CurvatureProxy,
    bool IsValid);
