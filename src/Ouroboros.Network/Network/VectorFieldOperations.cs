// <copyright file="VectorFieldOperations.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Network;

/// <summary>
/// Provides discrete differential geometry operations on MerkleDag graphs with vector embeddings.
/// Implements divergence and rotation (curl) computations for semantic flow analysis.
/// </summary>
public static class VectorFieldOperations
{
    /// <summary>
    /// Computes the discrete divergence at a node, representing net semantic outflow.
    /// Positive divergence indicates a semantic source (generating new meaning),
    /// negative divergence indicates a semantic sink (consolidating meaning).
    /// </summary>
    /// <param name="dag">The MerkleDag to analyze.</param>
    /// <param name="nodeId">The node at which to compute divergence.</param>
    /// <param name="getEmbedding">Function to retrieve embeddings for nodes.</param>
    /// <returns>The divergence value, or 0 if node has no edges.</returns>
    public static float ComputeDivergence(
        MerkleDag dag,
        Guid nodeId,
        Func<Guid, float[]> getEmbedding)
    {
        if (dag == null) throw new ArgumentNullException(nameof(dag));
        if (getEmbedding == null) throw new ArgumentNullException(nameof(getEmbedding));

        var nodeOption = dag.GetNode(nodeId);
        if (!nodeOption.HasValue)
        {
            return 0f;
        }

        var nodeEmbedding = getEmbedding(nodeId);
        if (nodeEmbedding == null || nodeEmbedding.Length == 0)
        {
            return 0f;
        }

        float divergence = 0f;

        // Outflow: sum cosine similarities with output neighbors
        var outgoingEdges = dag.GetOutgoingEdges(nodeId);
        foreach (var edge in outgoingEdges)
        {
            var outputEmbedding = getEmbedding(edge.OutputId);
            if (outputEmbedding != null && outputEmbedding.Length == nodeEmbedding.Length)
            {
                divergence += CosineSimilarity(nodeEmbedding, outputEmbedding);
            }
        }

        // Inflow: subtract cosine similarities with input neighbors
        var incomingEdges = dag.GetIncomingEdges(nodeId);
        foreach (var edge in incomingEdges)
        {
            foreach (var inputId in edge.InputIds)
            {
                var inputEmbedding = getEmbedding(inputId);
                if (inputEmbedding != null && inputEmbedding.Length == nodeEmbedding.Length)
                {
                    divergence -= CosineSimilarity(nodeEmbedding, inputEmbedding);
                }
            }
        }

        return divergence;
    }

    /// <summary>
    /// Computes the discrete rotation (curl) around a node, representing cyclic semantic flow.
    /// High rotation indicates reasoning loops or cyclic patterns in the semantic space.
    /// </summary>
    /// <param name="dag">The MerkleDag to analyze.</param>
    /// <param name="nodeId">The node at which to compute rotation.</param>
    /// <param name="getEmbedding">Function to retrieve embeddings for nodes.</param>
    /// <returns>The rotation magnitude, or 0 if insufficient neighbors.</returns>
    public static float ComputeRotation(
        MerkleDag dag,
        Guid nodeId,
        Func<Guid, float[]> getEmbedding)
    {
        if (dag == null) throw new ArgumentNullException(nameof(dag));
        if (getEmbedding == null) throw new ArgumentNullException(nameof(getEmbedding));

        var neighbors = GetOrderedNeighbors(dag, nodeId);
        if (neighbors.Count < 2)
        {
            return 0f;
        }

        float totalRotation = 0f;

        // Compute cross-product magnitude between consecutive neighbor pairs
        for (int i = 0; i < neighbors.Count; i++)
        {
            var currentEmbedding = getEmbedding(neighbors[i]);
            var nextEmbedding = getEmbedding(neighbors[(i + 1) % neighbors.Count]);

            if (currentEmbedding != null && nextEmbedding != null &&
                currentEmbedding.Length == nextEmbedding.Length)
            {
                totalRotation += CrossProductMagnitude(currentEmbedding, nextEmbedding);
            }
        }

        return totalRotation / neighbors.Count;
    }

    /// <summary>
    /// Computes divergence for all nodes in the DAG.
    /// </summary>
    /// <param name="dag">The MerkleDag to analyze.</param>
    /// <param name="getEmbedding">Function to retrieve embeddings for nodes.</param>
    /// <returns>A read-only dictionary mapping node IDs to divergence values.</returns>
    public static IReadOnlyDictionary<Guid, float> ComputeAllDivergences(
        MerkleDag dag,
        Func<Guid, float[]> getEmbedding)
    {
        if (dag == null) throw new ArgumentNullException(nameof(dag));
        if (getEmbedding == null) throw new ArgumentNullException(nameof(getEmbedding));

        var result = new Dictionary<Guid, float>();

        foreach (var node in dag.Nodes.Values)
        {
            result[node.Id] = ComputeDivergence(dag, node.Id, getEmbedding);
        }

        return result;
    }

    /// <summary>
    /// Computes rotation for all nodes in the DAG.
    /// </summary>
    /// <param name="dag">The MerkleDag to analyze.</param>
    /// <param name="getEmbedding">Function to retrieve embeddings for nodes.</param>
    /// <returns>A read-only dictionary mapping node IDs to rotation values.</returns>
    public static IReadOnlyDictionary<Guid, float> ComputeAllRotations(
        MerkleDag dag,
        Func<Guid, float[]> getEmbedding)
    {
        if (dag == null) throw new ArgumentNullException(nameof(dag));
        if (getEmbedding == null) throw new ArgumentNullException(nameof(getEmbedding));

        var result = new Dictionary<Guid, float>();

        foreach (var node in dag.Nodes.Values)
        {
            result[node.Id] = ComputeRotation(dag, node.Id, getEmbedding);
        }

        return result;
    }

    /// <summary>
    /// Computes cosine similarity between two vectors.
    /// Returns 0 if either vector is null, empty, or has zero magnitude.
    /// </summary>
    /// <param name="a">First vector.</param>
    /// <param name="b">Second vector.</param>
    /// <returns>Cosine similarity in range [-1, 1].</returns>
    public static float CosineSimilarity(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length == 0 || b.Length == 0 || a.Length != b.Length)
        {
            return 0f;
        }

        double dotProduct = 0.0;
        double magnitudeA = 0.0;
        double magnitudeB = 0.0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = Math.Sqrt(magnitudeA);
        magnitudeB = Math.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
        {
            return 0f;
        }

        return (float)(dotProduct / (magnitudeA * magnitudeB));
    }

    /// <summary>
    /// Computes the magnitude of the cross product between two vectors.
    /// For high-dimensional vectors, uses the first 3 dimensions.
    /// </summary>
    /// <param name="a">First vector.</param>
    /// <param name="b">Second vector.</param>
    /// <returns>Cross product magnitude.</returns>
    public static float CrossProductMagnitude(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length < 3 || b.Length < 3)
        {
            return 0f;
        }

        // Use first 3 dimensions for cross product
        double crossX = (a[1] * b[2]) - (a[2] * b[1]);
        double crossY = (a[2] * b[0]) - (a[0] * b[2]);
        double crossZ = (a[0] * b[1]) - (a[1] * b[0]);

        return (float)Math.Sqrt((crossX * crossX) + (crossY * crossY) + (crossZ * crossZ));
    }

    /// <summary>
    /// Gets ordered neighbors of a node by combining incoming and outgoing edge neighbors.
    /// </summary>
    /// <param name="dag">The MerkleDag to query.</param>
    /// <param name="nodeId">The node ID.</param>
    /// <returns>An ordered list of neighbor node IDs.</returns>
    public static IReadOnlyList<Guid> GetOrderedNeighbors(MerkleDag dag, Guid nodeId)
    {
        if (dag == null) throw new ArgumentNullException(nameof(dag));

        var neighbors = new List<Guid>();

        // Add output neighbors (from outgoing edges)
        var outgoingEdges = dag.GetOutgoingEdges(nodeId);
        foreach (var edge in outgoingEdges)
        {
            if (!neighbors.Contains(edge.OutputId))
            {
                neighbors.Add(edge.OutputId);
            }
        }

        // Add input neighbors (from incoming edges)
        var incomingEdges = dag.GetIncomingEdges(nodeId);
        foreach (var edge in incomingEdges)
        {
            foreach (var inputId in edge.InputIds)
            {
                if (!neighbors.Contains(inputId))
                {
                    neighbors.Add(inputId);
                }
            }
        }

        return neighbors;
    }
}
