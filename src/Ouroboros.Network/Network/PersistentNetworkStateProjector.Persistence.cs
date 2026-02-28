// <copyright file="PersistentNetworkStateProjector.Persistence.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Network;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Qdrant.Client.Grpc;

/// <summary>
/// Partial class containing Qdrant persistence operations (load, save, collections, dispose).
/// </summary>
public sealed partial class PersistentNetworkStateProjector
{
    private async Task EnsureCollectionsExistAsync(CancellationToken ct)
    {
        try
        {
            await EnsureCollectionWithDimensionAsync(_snapshotCollectionName, ct);
            await EnsureCollectionWithDimensionAsync(_learningsCollectionName, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to create Qdrant collections");
        }
    }

    private async Task EnsureCollectionWithDimensionAsync(string collectionName, CancellationToken ct)
    {
        var vectorParams = new VectorParams { Size = (ulong)_detectedVectorDimension, Distance = Distance.Cosine };

        if (await _qdrantClient.CollectionExistsAsync(collectionName, ct))
        {
            var info = await _qdrantClient.GetCollectionInfoAsync(collectionName, ct);
            var currentDim = info.Config?.Params?.VectorsConfig?.Params?.Size;
            if (currentDim.HasValue && currentDim.Value != (ulong)_detectedVectorDimension)
            {
                _logger.LogInformation("Dimension mismatch in {CollectionName} ({CurrentDim} vs {ExpectedDim}), recreating...", collectionName, currentDim, _detectedVectorDimension);
                await _qdrantClient.DeleteCollectionAsync(collectionName, cancellationToken: ct);
                await _qdrantClient.CreateCollectionAsync(collectionName, vectorParams, cancellationToken: ct);
            }
        }
        else
        {
            await _qdrantClient.CreateCollectionAsync(collectionName, vectorParams, cancellationToken: ct);
        }
    }

    private async Task LoadPreviousStateAsync(CancellationToken ct)
    {
        try
        {
            var scrollResult = await _qdrantClient.ScrollAsync(
                _snapshotCollectionName,
                limit: DefaultScrollLimit,
                cancellationToken: ct);

            GlobalNetworkState? latestSnapshot = null;
            long maxEpoch = -1;

            foreach (var point in scrollResult.Result)
            {
                if (point.Payload.TryGetValue("snapshot_json", out var jsonValue))
                {
                    var snapshot = JsonSerializer.Deserialize<GlobalNetworkState>(jsonValue.StringValue);
                    if (snapshot != null)
                    {
                        _snapshots.Add(snapshot);
                        if (snapshot.Epoch > maxEpoch)
                        {
                            maxEpoch = snapshot.Epoch;
                            latestSnapshot = snapshot;
                        }
                    }
                }
            }

            if (latestSnapshot != null)
            {
                _currentEpoch = latestSnapshot.Epoch + 1;
                _logger.LogInformation("Resumed from epoch {Epoch} ({SnapshotCount} snapshots loaded)", latestSnapshot.Epoch, _snapshots.Count);
            }

            var learningsResult = await _qdrantClient.ScrollAsync(
                _learningsCollectionName,
                limit: DefaultScrollLimit,
                cancellationToken: ct);

            foreach (var point in learningsResult.Result)
            {
                if (point.Payload.TryGetValue("learning_json", out var jsonValue))
                {
                    var learning = JsonSerializer.Deserialize<Learning>(jsonValue.StringValue);
                    if (learning != null)
                    {
                        _recentLearnings.Add(learning);
                    }
                }
            }

            if (_recentLearnings.Count > 0)
            {
                _logger.LogInformation("Loaded {LearningCount} previous learnings", _recentLearnings.Count);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to load previous state");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to load previous state");
        }
    }

    private async Task PersistSnapshotAsync(GlobalNetworkState state, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(state);
            var embedding = await _embeddingFunc($"network state epoch {state.Epoch} nodes {state.TotalNodes} transitions {state.TotalTransitions}");

            var point = new PointStruct
            {
                Id = new PointId { Uuid = Guid.NewGuid().ToString() },
                Vectors = embedding,
                Payload =
                {
                    ["epoch"] = state.Epoch,
                    ["total_nodes"] = state.TotalNodes,
                    ["total_transitions"] = state.TotalTransitions,
                    ["timestamp"] = state.Timestamp.ToString("O"),
                    ["snapshot_json"] = json,
                },
            };

            await _qdrantClient.UpsertAsync(_snapshotCollectionName, new[] { point }, cancellationToken: ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to persist snapshot");
        }
    }

    private async Task PersistLearningAsync(Learning learning, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(learning);
            var embedding = await _embeddingFunc($"{learning.Category}: {learning.Content}");

            var point = new PointStruct
            {
                Id = new PointId { Uuid = learning.Id },
                Vectors = embedding,
                Payload =
                {
                    ["category"] = learning.Category,
                    ["content"] = learning.Content,
                    ["context"] = learning.Context,
                    ["confidence"] = learning.Confidence,
                    ["epoch"] = learning.Epoch,
                    ["timestamp"] = learning.Timestamp.ToString("O"),
                    ["learning_json"] = json,
                },
            };

            await _qdrantClient.UpsertAsync(_learningsCollectionName, new[] { point }, cancellationToken: ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to persist learning");
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_initialized && _dag.NodeCount > 0)
        {
            try
            {
                await ProjectAndPersistAsync(
                    ImmutableDictionary<string, string>.Empty.Add("event", "shutdown"));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Ignore errors during disposal
            }
        }

        _qdrantClient.Dispose();
    }
}
