// <copyright file="QdrantDagStore.Helpers.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Core.Configuration;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Match = Qdrant.Client.Grpc.Match;

namespace Ouroboros.Network;

public sealed partial class QdrantDagStore
{
    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (!_initialized)
        {
            await InitializeAsync(ct);
        }
    }

    private async Task<float[]> GenerateNodeEmbeddingAsync(MonadNode node)
    {
        if (_embeddingFunc == null)
        {
            // Return zero vector if no embedding function
            return new float[_config.VectorSize];
        }

        // Generate semantic text for embedding
        var semanticText = $"{node.TypeName}: {node.PayloadJson}";
        return await _embeddingFunc(semanticText);
    }

    private async Task<float[]> GenerateEdgeEmbeddingAsync(TransitionEdge edge)
    {
        if (_embeddingFunc == null)
        {
            return new float[_config.VectorSize];
        }

        var semanticText = $"{edge.OperationName}: {edge.OperationSpecJson}";
        return await _embeddingFunc(semanticText);
    }

    private async Task<Result<IReadOnlyList<MonadNode>>> LoadAllNodesAsync(CancellationToken ct)
    {
        try
        {
            var nodes = new List<MonadNode>();
            PointId? offset = null;

            while (true)
            {
                var scrollResponse = await _client.ScrollAsync(
                    _config.NodesCollection,
                    limit: 100,
                    offset: offset,
                    cancellationToken: ct);

                foreach (var point in scrollResponse.Result)
                {
                    var node = DeserializeNode(point.Payload);
                    if (node != null)
                    {
                        nodes.Add(node);
                    }
                }

                if (scrollResponse.NextPageOffset == null) break;
                offset = scrollResponse.NextPageOffset;
            }

            return Result<IReadOnlyList<MonadNode>>.Success(nodes);
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            return Result<IReadOnlyList<MonadNode>>.Failure($"Failed to load nodes: {ex.Message}");
        }
    }

    private async Task<Result<IReadOnlyList<TransitionEdge>>> LoadAllEdgesAsync(CancellationToken ct)
    {
        try
        {
            var edges = new List<TransitionEdge>();
            PointId? offset = null;

            while (true)
            {
                var scrollResponse = await _client.ScrollAsync(
                    _config.EdgesCollection,
                    limit: 100,
                    offset: offset,
                    cancellationToken: ct);

                foreach (var point in scrollResponse.Result)
                {
                    var edge = DeserializeEdge(point.Payload);
                    if (edge != null)
                    {
                        edges.Add(edge);
                    }
                }

                if (scrollResponse.NextPageOffset == null) break;
                offset = scrollResponse.NextPageOffset;
            }

            return Result<IReadOnlyList<TransitionEdge>>.Success(edges);
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            return Result<IReadOnlyList<TransitionEdge>>.Failure($"Failed to load edges: {ex.Message}");
        }
    }

    private static MonadNode? DeserializeNode(IDictionary<string, Value> payload)
    {
        try
        {
            var id = Guid.Parse(payload["id"].StringValue);
            var typeName = payload["type_name"].StringValue;
            var payloadJson = payload["payload_json"].StringValue;
            var createdAt = DateTimeOffset.Parse(payload["created_at"].StringValue);
            var parentIdsStr = payload["parent_ids"].StringValue;

            var parentIds = string.IsNullOrEmpty(parentIdsStr)
                ? ImmutableArray<Guid>.Empty
                : parentIdsStr.Split(',').Where(s => !string.IsNullOrEmpty(s)).Select(Guid.Parse).ToImmutableArray();

            return new MonadNode(id, typeName, payloadJson, createdAt, parentIds);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static TransitionEdge? DeserializeEdge(IDictionary<string, Value> payload)
    {
        try
        {
            var id = Guid.Parse(payload["id"].StringValue);
            var inputIdsStr = payload["input_ids"].StringValue;
            var outputId = Guid.Parse(payload["output_id"].StringValue);
            var operationName = payload["operation_name"].StringValue;
            var operationSpecJson = payload["operation_spec_json"].StringValue;
            var createdAt = DateTimeOffset.Parse(payload["created_at"].StringValue);

            var inputIds = inputIdsStr.Split(',').Where(s => !string.IsNullOrEmpty(s)).Select(Guid.Parse).ToImmutableArray();

            double? confidence = payload.TryGetValue("confidence", out var confVal) ? confVal.DoubleValue : null;
            long? durationMs = payload.TryGetValue("duration_ms", out var durVal) ? (long)durVal.IntegerValue : null;

            return new TransitionEdge(id, inputIds, outputId, operationName, operationSpecJson, createdAt, confidence, durationMs);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static IReadOnlyList<MonadNode> TopologicalSort(IReadOnlyList<MonadNode> nodes)
    {
        var nodeMap = nodes.ToDictionary(n => n.Id);
        var visited = new HashSet<Guid>();
        var result = new List<MonadNode>();

        void Visit(MonadNode node)
        {
            if (visited.Contains(node.Id)) return;
            visited.Add(node.Id);

            // Visit parents first
            foreach (var parentId in node.ParentIds)
            {
                if (nodeMap.TryGetValue(parentId, out var parent))
                {
                    Visit(parent);
                }
            }

            result.Add(node);
        }

        foreach (var node in nodes)
        {
            Visit(node);
        }

        return result;
    }
}
