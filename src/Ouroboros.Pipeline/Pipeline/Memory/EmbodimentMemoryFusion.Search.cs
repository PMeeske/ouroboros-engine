// <copyright file="EmbodimentMemoryFusion.Search.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Memory;

using Microsoft.Extensions.Logging;
using Ouroboros.Agent.MetaAI.Affect;
using Qdrant.Client.Grpc;

/// <summary>
/// Search, consolidation, and distribution operations for <see cref="EmbodimentMemoryFusion"/>.
/// </summary>
public sealed partial class EmbodimentMemoryFusion
{
    /// <inheritdoc/>
    public async Task<Result<List<ScoredMemory>, string>> HybridSearchAsync(
        string query,
        MemorySearchOptions? options = null,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(query);

            await EnsureCollectionInitializedAsync(ct);

            options ??= new MemorySearchOptions();

            var queryEmbedding = await _embeddingModel.CreateEmbeddingsAsync(query, ct);

            // Build payload filter from options
            var conditions = new List<Condition>();

            if (options.ModalityFilter is not null)
            {
                conditions.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "modality",
                        Match = new Match { Keyword = options.ModalityFilter },
                    },
                });
            }

            if (options.MinImportance.HasValue)
            {
                conditions.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "importance",
                        Range = new Range { Gte = options.MinImportance.Value },
                    },
                });
            }

            if (options.After.HasValue)
            {
                conditions.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "timestamp",
                        Range = new Range { Gte = options.After.Value.Ticks },
                    },
                });
            }

            if (options.Before.HasValue)
            {
                conditions.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "timestamp",
                        Range = new Range { Lte = options.Before.Value.Ticks },
                    },
                });
            }

            Filter? filter = conditions.Count > 0
                ? new Filter { Must = { conditions } }
                : null;

            // Fetch more than limit so we can rescore with temporal decay and keyword matching
            var fetchLimit = (ulong)Math.Min(options.Limit * 3, 100);

            var searchResult = await _qdrantClient.SearchAsync(
                _collectionName,
                queryEmbedding,
                filter: filter,
                limit: fetchLimit,
                cancellationToken: ct);

            var queryKeywords = options.UseSparseKeywords
                ? new HashSet<string>(ExtractKeywords(query))
                : null;

            var now = DateTime.UtcNow;

            var scored = searchResult
                .Select(point => RescorePoint(point, queryKeywords, now, options.TemporalDecayFactor))
                .OrderByDescending(m => m.Score)
                .Take(options.Limit)
                .ToList();

            _logger?.LogInformation(
                "Hybrid search returned {Count} results for query (filter conditions: {FilterCount})",
                scored.Count, conditions.Count);

            return Result<List<ScoredMemory>, string>.Success(scored);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to perform hybrid search");
            return Result<List<ScoredMemory>, string>.Failure($"Failed to perform hybrid search: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<List<ScoredMemory>, string>> CrossModalSearchAsync(
        string query,
        string sourceModality,
        string targetModality,
        int limit = 10,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(query);
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceModality);
            ArgumentException.ThrowIfNullOrWhiteSpace(targetModality);

            await EnsureCollectionInitializedAsync(ct);

            // Enrich query with source modality context for cross-modal bridging
            var enrichedQuery = $"[{sourceModality}] {query}";
            var queryEmbedding = await _embeddingModel.CreateEmbeddingsAsync(enrichedQuery, ct);

            // Filter to target modality
            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "modality",
                            Match = new Match { Keyword = targetModality },
                        },
                    },
                },
            };

            var searchResult = await _qdrantClient.SearchAsync(
                _collectionName,
                queryEmbedding,
                filter: filter,
                limit: (ulong)limit,
                cancellationToken: ct);

            var results = searchResult
                .Select(DeserializeScoredPoint)
                .ToList();

            _logger?.LogInformation(
                "Cross-modal search {Source}->{Target} returned {Count} results",
                sourceModality, targetModality, results.Count);

            return Result<List<ScoredMemory>, string>.Success(results);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to perform cross-modal search");
            return Result<List<ScoredMemory>, string>.Failure($"Failed to perform cross-modal search: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<ConsolidationResult, string>> ConsolidateMemoriesAsync(
        double similarityThreshold = 0.85,
        CancellationToken ct = default)
    {
        try
        {
            await EnsureCollectionInitializedAsync(ct);

            // Scroll through all memories in paginated fashion
            var allPoints = new List<RetrievedPoint>();
            PointId? nextOffset = null;

            while (true)
            {
                var scrollResult = await _qdrantClient.ScrollAsync(
                    _collectionName,
                    limit: 100,
                    offset: nextOffset,
                    cancellationToken: ct);

                allPoints.AddRange(scrollResult.Result);

                nextOffset = scrollResult.NextPageOffset;
                if (nextOffset is null || scrollResult.Result.Count == 0)
                {
                    break;
                }
            }

            if (allPoints.Count < 2)
            {
                return Result<ConsolidationResult, string>.Success(
                    new ConsolidationResult(allPoints.Count, 0, 0));
            }

            // Find high-similarity clusters by searching each point
            var merged = new HashSet<string>();
            var clustersMerged = 0;
            var newAbstractions = 0;

            foreach (var point in allPoints)
            {
                var pointUuid = point.Id.Uuid;
                if (merged.Contains(pointUuid))
                {
                    continue;
                }

                var vector = point.Vectors.Vector.Data.ToArray();

                var neighbors = await _qdrantClient.SearchAsync(
                    _collectionName,
                    vector,
                    limit: 10,
                    scoreThreshold: (float)similarityThreshold,
                    cancellationToken: ct);

                // Exclude self and already-merged points
                var cluster = neighbors
                    .Where(n => n.Id.Uuid != pointUuid && !merged.Contains(n.Id.Uuid))
                    .ToList();

                if (cluster.Count == 0)
                {
                    continue;
                }

                // Merge cluster into an abstraction point
                var clusterContents = new List<string> { point.Payload["content"].StringValue };
                clusterContents.AddRange(cluster.Select(n => n.Payload["content"].StringValue));

                var abstractionContent = $"[abstraction] {string.Join(" | ", clusterContents.Take(5))}";
                var abstractionEmbedding = await _embeddingModel.CreateEmbeddingsAsync(abstractionContent, ct);

                // Use the highest importance from the cluster
                var maxImportance = Math.Max(
                    point.Payload["importance"].DoubleValue,
                    cluster.Max(n => n.Payload["importance"].DoubleValue));

                var abstractionId = Guid.NewGuid();
                var abstractionPoint = new PointStruct
                {
                    Id = new PointId { Uuid = abstractionId.ToString() },
                    Vectors = abstractionEmbedding,
                    Payload =
                    {
                        ["content"] = abstractionContent,
                        ["modality"] = "abstraction",
                        ["importance"] = maxImportance,
                        ["timestamp"] = DateTime.UtcNow.Ticks,
                        ["keywords"] = JsonSerializer.Serialize(ExtractKeywords(abstractionContent)),
                        ["metadata"] = JsonSerializer.Serialize(new Dictionary<string, string>
                        {
                            ["consolidated"] = "true",
                            ["source_count"] = (cluster.Count + 1).ToString(),
                        }),
                    },
                };

                await _qdrantClient.UpsertAsync(
                    _collectionName, new[] { abstractionPoint }, cancellationToken: ct);

                // Delete merged (non-anchor) points
                var toDelete = cluster
                    .Select(n => new PointId { Uuid = n.Id.Uuid })
                    .ToList();

                await _qdrantClient.DeleteAsync(_collectionName, toDelete, cancellationToken: ct);

                foreach (var n in cluster)
                {
                    merged.Add(n.Id.Uuid);
                }

                clustersMerged++;
                newAbstractions++;
            }

            _logger?.LogInformation(
                "Consolidation complete: {Processed} processed, {Clusters} clusters merged, {Abstractions} new abstractions",
                allPoints.Count, clustersMerged, newAbstractions);

            return Result<ConsolidationResult, string>.Success(
                new ConsolidationResult(allPoints.Count, clustersMerged, newAbstractions));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to consolidate memories");
            return Result<ConsolidationResult, string>.Failure($"Failed to consolidate memories: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<MemoryDistribution, string>> GetMemoryDistributionAsync(
        CancellationToken ct = default)
    {
        try
        {
            await EnsureCollectionInitializedAsync(ct);

            var totalMemories = 0;
            var totalImportance = 0.0;
            var high = 0;
            var medium = 0;
            var low = 0;
            var byModality = new Dictionary<string, int>();
            PointId? nextOffset = null;

            while (true)
            {
                var scrollResult = await _qdrantClient.ScrollAsync(
                    _collectionName,
                    limit: 100,
                    offset: nextOffset,
                    cancellationToken: ct);

                foreach (var point in scrollResult.Result)
                {
                    totalMemories++;
                    var importance = point.Payload["importance"].DoubleValue;
                    totalImportance += importance;

                    if (importance >= 0.7)
                    {
                        high++;
                    }
                    else if (importance >= 0.3)
                    {
                        medium++;
                    }
                    else
                    {
                        low++;
                    }

                    var modality = point.Payload["modality"].StringValue;
                    if (!byModality.TryGetValue(modality, out var count))
                    {
                        count = 0;
                    }

                    byModality[modality] = count + 1;
                }

                nextOffset = scrollResult.NextPageOffset;
                if (nextOffset is null || scrollResult.Result.Count == 0)
                {
                    break;
                }
            }

            var avgImportance = totalMemories > 0 ? totalImportance / totalMemories : 0.0;

            return Result<MemoryDistribution, string>.Success(
                new MemoryDistribution(totalMemories, avgImportance, high, medium, low, byModality));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get memory distribution");
            return Result<MemoryDistribution, string>.Failure($"Failed to get memory distribution: {ex.Message}");
        }
    }

    private ScoredMemory RescorePoint(
        ScoredPoint point,
        HashSet<string>? queryKeywords,
        DateTime now,
        double temporalDecayFactor)
    {
        var baseScore = point.Score;
        var ticks = (long)point.Payload["timestamp"].IntegerValue;
        var timestamp = new DateTime(ticks, DateTimeKind.Utc);
        var importance = point.Payload["importance"].DoubleValue;

        // Temporal decay boost: more recent memories score higher
        var ageDays = Math.Max(0, (now - timestamp).TotalDays);
        var temporalBoost = Math.Exp(-temporalDecayFactor * ageDays);

        // Keyword overlap boost
        var keywordBoost = 0.0;
        if (queryKeywords is { Count: > 0 })
        {
            try
            {
                var storedKeywords = JsonSerializer.Deserialize<string[]>(
                    point.Payload["keywords"].StringValue) ?? Array.Empty<string>();
                var overlap = queryKeywords.Intersect(storedKeywords).Count();
                keywordBoost = queryKeywords.Count > 0
                    ? 0.1 * ((double)overlap / queryKeywords.Count)
                    : 0.0;
            }
            catch
            {
                // Ignore keyword parsing failures
            }
        }

        // Combined score: 70% vector similarity, 15% temporal, 10% keyword, 5% importance
        var combinedScore = (0.70 * baseScore)
                          + (0.15 * temporalBoost)
                          + (0.10 * keywordBoost)
                          + (0.05 * importance);

        return DeserializeScoredPointWithScore(point, combinedScore);
    }

    private static ScoredMemory DeserializeScoredPoint(ScoredPoint point)
    {
        return DeserializeScoredPointWithScore(point, point.Score);
    }

    private static ScoredMemory DeserializeScoredPointWithScore(ScoredPoint point, double score)
    {
        var id = Guid.Parse(point.Id.Uuid);
        var content = point.Payload["content"].StringValue;
        var modality = point.Payload["modality"].StringValue;
        var importance = point.Payload["importance"].DoubleValue;
        var ticks = (long)point.Payload["timestamp"].IntegerValue;
        var timestamp = new DateTime(ticks, DateTimeKind.Utc);

        Dictionary<string, string>? metadata = null;
        if (point.Payload.TryGetValue("metadata", out var metadataValue)
            && !string.IsNullOrEmpty(metadataValue.StringValue))
        {
            try
            {
                metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataValue.StringValue);
            }
            catch
            {
                // Ignore metadata deserialization failures
            }
        }

        return new ScoredMemory(id, content, modality, score, importance, timestamp, metadata);
    }
}
