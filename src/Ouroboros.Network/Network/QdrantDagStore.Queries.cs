// <copyright file="QdrantDagStore.Queries.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Qdrant.Client.Grpc;
using Match = Qdrant.Client.Grpc.Match;

namespace Ouroboros.Network;

public sealed partial class QdrantDagStore
{
    /// <summary>
    /// Searches for nodes semantically similar to the query.
    /// </summary>
    public async Task<Result<IReadOnlyList<ScoredNode>>> SearchNodesAsync(
        string query,
        int limit = 10,
        CancellationToken ct = default)
    {
        if (!SupportsSemanticSearch)
        {
            return Result<IReadOnlyList<ScoredNode>>.Failure("Semantic search requires an embedding function");
        }

        await EnsureInitializedAsync(ct).ConfigureAwait(false);

        try
        {
            var embedding = await _embeddingFunc!(query).ConfigureAwait(false);

            var results = await _client.SearchAsync(
                _config.NodesCollection,
                embedding,
                limit: (ulong)limit,
                cancellationToken: ct).ConfigureAwait(false);

            var scoredNodes = new List<ScoredNode>();
            foreach (var result in results)
            {
                var node = DeserializeNode(result.Payload);
                if (node != null)
                {
                    scoredNodes.Add(new ScoredNode(node, result.Score));
                }
            }

            return Result<IReadOnlyList<ScoredNode>>.Success(scoredNodes);
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            return Result<IReadOnlyList<ScoredNode>>.Failure($"Search failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Searches for nodes by type name.
    /// </summary>
    public async Task<Result<IReadOnlyList<MonadNode>>> GetNodesByTypeAsync(
        string typeName,
        int limit = 100,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct).ConfigureAwait(false);

        try
        {
            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "type_name",
                            Match = new Match { Keyword = typeName },
                        },
                    },
                },
            };

            var scrollResponse = await _client.ScrollAsync(
                _config.NodesCollection,
                filter: filter,
                limit: (uint)limit,
                cancellationToken: ct).ConfigureAwait(false);

            var nodes = scrollResponse.Result
                .Select(r => DeserializeNode(r.Payload))
                .Where(n => n != null)
                .Cast<MonadNode>()
                .ToList();

            return Result<IReadOnlyList<MonadNode>>.Success(nodes);
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            return Result<IReadOnlyList<MonadNode>>.Failure($"Query failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a specific node by ID.
    /// </summary>
    public async Task<Option<MonadNode>> GetNodeByIdAsync(Guid nodeId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct).ConfigureAwait(false);

        try
        {
            var results = await _client.RetrieveAsync(
                _config.NodesCollection,
                new[] { new PointId { Uuid = nodeId.ToString() } },
                withPayload: true,
                cancellationToken: ct).ConfigureAwait(false);

            var point = results.FirstOrDefault();
            if (point == null)
            {
                return Option<MonadNode>.None;
            }

            var node = DeserializeNode(point.Payload);
            return node != null ? Option<MonadNode>.Some(node) : Option<MonadNode>.None;
        }
        catch (Grpc.Core.RpcException)
        {
            return Option<MonadNode>.None;
        }
    }

    /// <summary>
    /// Deletes all DAG data from Qdrant.
    /// </summary>
    public async Task ClearAsync(CancellationToken ct = default)
    {
        try
        {
            if (await _client.CollectionExistsAsync(_config.NodesCollection, ct).ConfigureAwait(false))
            {
                await _client.DeleteCollectionAsync(_config.NodesCollection, cancellationToken: ct).ConfigureAwait(false);
            }

            if (await _client.CollectionExistsAsync(_config.EdgesCollection, ct).ConfigureAwait(false))
            {
                await _client.DeleteCollectionAsync(_config.EdgesCollection, cancellationToken: ct).ConfigureAwait(false);
            }

            _initialized = false;
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            throw new InvalidOperationException($"Failed to clear DAG data: {ex.Message}", ex);
        }
    }
}
