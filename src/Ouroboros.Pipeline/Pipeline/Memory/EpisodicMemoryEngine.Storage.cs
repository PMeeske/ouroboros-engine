// <copyright file="EpisodicMemoryEngine.Storage.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Memory;

using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ouroboros.Pipeline.Json;
using Ouroboros.Domain;
using Ouroboros.Domain.States;
using Ouroboros.Pipeline.Verification;
using Qdrant.Client.Grpc;

public sealed partial class EpisodicMemoryEngine
{
    /// <inheritdoc/>
    public async Task<Result<EpisodeId, string>> StoreEpisodeAsync(
        PipelineBranch branch,
        PipelineExecutionContext context,
        Outcome result,
        ImmutableDictionary<string, object> metadata,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(branch);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(metadata);

            // Ensure collection exists
            await EnsureCollectionInitializedAsync(ct);

            // Create episode ID
            var episodeId = Guid.NewGuid();

            // Generate embedding from goal and outcome
            var embeddingText = $"{context.Goal}\n{result.Output}";
            var embedding = await _embeddingModel.CreateEmbeddingsAsync(embeddingText, ct);

            // Serialize pipeline branch
            var branchJson = SerializePipelineBranch(branch);

            // Calculate success score
            var successScore = result.Success ? 1.0 : Math.Max(0.0, 1.0 - (result.Errors.Count * 0.2));

            // Extract lessons learned from reasoning events
            var lessonsLearned = ExtractLessonsLearned(branch);

            // Merge context metadata with provided metadata
            var combinedContext = context.Metadata.SetItems(metadata);

            // Create episode
            var episode = new Episode(
                episodeId,
                DateTime.UtcNow,
                context.Goal,
                branch,
                result,
                successScore,
                lessonsLearned,
                combinedContext,
                embedding);

            // Store in Qdrant
            var point = new PointStruct
            {
                Id = new PointId { Uuid = episodeId.ToString() },
                Vectors = embedding,
                Payload =
                {
                    ["goal"] = context.Goal,
                    ["timestamp"] = episode.Timestamp.ToString("o"),
                    ["success"] = result.Success,
                    ["success_score"] = successScore,
                    ["duration_ms"] = result.Duration.TotalMilliseconds,
                    ["output"] = result.Output,
                    ["errors"] = JsonSerializer.Serialize(result.Errors),
                    ["lessons_learned"] = JsonSerializer.Serialize(lessonsLearned),
                    ["branch_json"] = branchJson,
                    ["context"] = JsonSerializer.Serialize(combinedContext),
                },
            };

            await _qdrantClient.UpsertAsync(_collectionName, new[] { point }, cancellationToken: ct);

            _logger?.LogInformation("Stored episode {EpisodeId} with goal: {Goal}", episodeId, context.Goal);

            return Result<EpisodeId, string>.Success(new EpisodeId(episodeId));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to store episode");
            return Result<EpisodeId, string>.Failure($"Failed to store episode: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, string>> ConsolidateMemoriesAsync(
        TimeSpan olderThan,
        ConsolidationStrategy strategy,
        CancellationToken ct = default)
    {
        try
        {
            await EnsureCollectionInitializedAsync(ct);

            var cutoffTime = DateTime.UtcNow - olderThan;
            _logger?.LogInformation(
                "Starting memory consolidation with strategy {Strategy} for episodes older than {CutoffTime}",
                strategy,
                cutoffTime);

            // Paginated scroll through all episodes, filtering by cutoff in memory.
            // Uses cursor-based pagination (NextPageOffset) to avoid artificial ceilings.
            var oldEpisodes = new List<Episode>();
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
                    var episodeOption = DeserializeEpisodeFromRetrieved(point);
                    if (episodeOption.HasValue && episodeOption.Value!.Timestamp < cutoffTime)
                    {
                        oldEpisodes.Add(episodeOption.Value!);
                    }
                }

                nextOffset = scrollResult.NextPageOffset;
                if (nextOffset is null || scrollResult.Result.Count == 0)
                {
                    break;
                }
            }

            if (oldEpisodes.Count == 0)
            {
                _logger?.LogInformation("No episodes found for consolidation");
                return Result<Unit, string>.Success(Unit.Value);
            }

            // Apply consolidation strategy
            var consolidationResult = strategy switch
            {
                ConsolidationStrategy.Compress => await ConsolidateByCompressionAsync(oldEpisodes, ct),
                ConsolidationStrategy.Abstract => await ConsolidateByAbstractionAsync(oldEpisodes, ct),
                ConsolidationStrategy.Prune => await ConsolidateByPruningAsync(oldEpisodes, ct),
                ConsolidationStrategy.Hierarchical => await ConsolidateHierarchicallyAsync(oldEpisodes, ct),
                _ => Result<Unit, string>.Failure($"Unknown consolidation strategy: {strategy}"),
            };

            return consolidationResult;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to consolidate memories");
            return Result<Unit, string>.Failure($"Failed to consolidate memories: {ex.Message}");
        }
    }

    private async Task EnsureCollectionInitializedAsync(CancellationToken ct)
    {
        if (_collectionInitialized)
        {
            return;
        }

        await _collectionInitLock.WaitAsync(ct);
        try
        {
            if (_collectionInitialized)
            {
                return;
            }

            // Check if collection exists
            var exists = await _qdrantClient.CollectionExistsAsync(_collectionName, ct);

            if (!exists)
            {
                _logger?.LogInformation("Creating collection: {CollectionName}", _collectionName);

                // Create collection with appropriate vector size (assuming 768 for typical embeddings)
                await _qdrantClient.CreateCollectionAsync(
                    _collectionName,
                    new VectorParams { Size = 768, Distance = Distance.Cosine },
                    cancellationToken: ct);
            }

            _collectionInitialized = true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize collection");
            throw;
        }
        finally
        {
            _collectionInitLock.Release();
        }
    }

    private string SerializePipelineBranch(PipelineBranch branch)
    {
        try
        {
            return JsonSerializer.Serialize(branch, JsonDefaults.Default);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to serialize pipeline branch, storing minimal info");
            return $"{{\"name\":\"{branch.Name}\",\"event_count\":{branch.Events.Count}}}";
        }
    }

    private ImmutableList<string> ExtractLessonsLearned(PipelineBranch branch)
    {
        var lessons = new List<string>();

        // Extract insights from reasoning steps
        var reasoningSteps = branch.Events.OfType<ReasoningStep>().ToList();

        foreach (var step in reasoningSteps)
        {
            // Look for critique and final spec states that often contain insights
            if (step.State is Critique critique)
            {
                if (!string.IsNullOrWhiteSpace(critique.Text) && critique.Text.Length > 20)
                {
                    lessons.Add($"Critique insight: {critique.Text.Substring(0, Math.Min(200, critique.Text.Length))}");
                }
            }
            else if (step.State is FinalSpec finalSpec)
            {
                if (!string.IsNullOrWhiteSpace(finalSpec.Text) && finalSpec.Text.Length > 20)
                {
                    lessons.Add($"Final insight: {finalSpec.Text.Substring(0, Math.Min(200, finalSpec.Text.Length))}");
                }
            }
        }

        // If no lessons found, add a generic one
        if (lessons.Count == 0 && reasoningSteps.Count > 0)
        {
            lessons.Add($"Completed reasoning with {reasoningSteps.Count} steps");
        }

        return lessons.ToImmutableList();
    }

    private async Task<Result<Unit, string>> ConsolidateByCompressionAsync(List<Episode> episodes, CancellationToken ct)
    {
        // Group similar episodes and create compressed summaries
        var groupedByGoal = episodes.GroupBy(ep => ep.Goal.ToLowerInvariant()).ToList();

        _logger?.LogInformation(
            "Compressing {EpisodeCount} episodes into {GroupCount} groups",
            episodes.Count,
            groupedByGoal.Count);

        foreach (var group in groupedByGoal)
        {
            var groupList = group.ToList();

            // Only compress groups with more than 3 episodes
            if (groupList.Count > 3)
            {
                _logger?.LogDebug("Group '{Goal}' has {Count} episodes to compress", group.Key, groupList.Count);

                // Keep the highest-scoring episode as the representative
                var representative = groupList.OrderByDescending(ep => ep.SuccessScore).First();
                var toMerge = groupList.Where(ep => ep.Id != representative.Id).ToList();

                // Merge lessons from other episodes into the representative
                var mergedLessons = representative.LessonsLearned
                    .AddRange(toMerge.SelectMany(ep => ep.LessonsLearned))
                    .Distinct()
                    .ToImmutableList();

                // Update context metadata to note this is a compressed summary
                var updatedContext = representative.Context
                    .SetItem("consolidated", (object)"compressed")
                    .SetItem("merged_episode_count", (object)groupList.Count)
                    .SetItem("merged_episode_ids", (object)string.Join(",", toMerge.Select(ep => ep.Id)));

                // Re-create the representative episode with merged data
                var compressedEpisode = representative with
                {
                    LessonsLearned = mergedLessons,
                    Context = updatedContext,
                };

                // Upsert the updated representative
                var point = new PointStruct
                {
                    Id = new PointId { Uuid = compressedEpisode.Id.ToString() },
                    Vectors = compressedEpisode.Embedding,
                    Payload =
                    {
                        ["goal"] = compressedEpisode.Goal,
                        ["timestamp"] = compressedEpisode.Timestamp.ToString("o"),
                        ["success"] = compressedEpisode.Result.Success,
                        ["success_score"] = compressedEpisode.SuccessScore,
                        ["duration_ms"] = compressedEpisode.Result.Duration.TotalMilliseconds,
                        ["output"] = compressedEpisode.Result.Output,
                        ["errors"] = JsonSerializer.Serialize(compressedEpisode.Result.Errors),
                        ["lessons_learned"] = JsonSerializer.Serialize(mergedLessons),
                        ["branch_json"] = SerializePipelineBranch(compressedEpisode.ReasoningTrace),
                        ["context"] = JsonSerializer.Serialize(updatedContext),
                    },
                };

                await _qdrantClient.UpsertAsync(_collectionName, new[] { point }, cancellationToken: ct);

                // Delete the merged (non-representative) episodes
                var pointIdsToDelete = toMerge
                    .Select(ep => new PointId { Uuid = ep.Id.ToString() })
                    .ToList();

                await _qdrantClient.DeleteAsync(_collectionName, pointIdsToDelete, cancellationToken: ct);

                _logger?.LogInformation(
                    "Compressed group '{Goal}': kept episode {RepId}, merged {MergedCount} episodes, {LessonCount} total lessons",
                    group.Key,
                    representative.Id,
                    toMerge.Count,
                    mergedLessons.Count);
            }
        }

        return Result<Unit, string>.Success(Unit.Value);
    }

    private async Task<Result<Unit, string>> ConsolidateByAbstractionAsync(List<Episode> episodes, CancellationToken ct)
    {
        // Extract patterns and rules from episodes
        var patterns = episodes
            .SelectMany(ep => ep.LessonsLearned)
            .GroupBy(lesson => lesson)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => new { Pattern = g.Key, Frequency = g.Count() })
            .ToList();

        _logger?.LogInformation(
            "Extracted {PatternCount} patterns from {EpisodeCount} episodes",
            patterns.Count,
            episodes.Count);

        if (patterns.Count == 0)
        {
            return Result<Unit, string>.Success(Unit.Value);
        }

        // Create a meta-episode containing the top patterns
        var metaEpisodeId = Guid.NewGuid();
        var patternLessons = patterns
            .Select(p => $"[freq={p.Frequency}] {p.Pattern}")
            .ToImmutableList();

        var patternContent = string.Join("\n", patterns.Select(p => $"{p.Pattern} (frequency: {p.Frequency})"));

        // Generate embedding from the pattern content
        var embeddingText = $"abstracted_patterns\n{patternContent}";
        var embedding = await _embeddingModel.CreateEmbeddingsAsync(embeddingText, ct);

        var metaContext = ImmutableDictionary<string, object>.Empty
            .Add("consolidated", (object)"abstracted")
            .Add("source_episode_count", (object)episodes.Count)
            .Add("pattern_frequencies", (object)JsonSerializer.Serialize(
                patterns.ToDictionary(p => p.Pattern, p => p.Frequency)));

        // Upsert the meta-episode to the collection
        var point = new PointStruct
        {
            Id = new PointId { Uuid = metaEpisodeId.ToString() },
            Vectors = embedding,
            Payload =
            {
                ["goal"] = "abstracted_patterns",
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["success"] = true,
                ["success_score"] = 1.0,
                ["duration_ms"] = 0.0,
                ["output"] = patternContent,
                ["errors"] = "[]",
                ["lessons_learned"] = JsonSerializer.Serialize(patternLessons),
                ["branch_json"] = "{}",
                ["context"] = JsonSerializer.Serialize(metaContext),
            },
        };

        await _qdrantClient.UpsertAsync(_collectionName, new[] { point }, cancellationToken: ct);

        _logger?.LogInformation(
            "Stored abstracted meta-episode {MetaId} with {PatternCount} patterns from {EpisodeCount} source episodes",
            metaEpisodeId,
            patterns.Count,
            episodes.Count);

        return Result<Unit, string>.Success(Unit.Value);
    }

    private async Task<Result<Unit, string>> ConsolidateByPruningAsync(List<Episode> episodes, CancellationToken ct)
    {
        // Remove low-value episodes (low success score, no unique lessons)
        var episodesToDelete = episodes
            .Where(ep => ep.SuccessScore < 0.3 && ep.LessonsLearned.Count == 0)
            .Select(ep => ep.Id)
            .ToList();

        if (episodesToDelete.Count > 0)
        {
            _logger?.LogInformation("Pruning {Count} low-value episodes", episodesToDelete.Count);

            var pointIds = episodesToDelete.Select(id => new PointId { Uuid = id.ToString() }).ToList();
            await _qdrantClient.DeleteAsync(_collectionName, pointIds, cancellationToken: ct);
        }

        return Result<Unit, string>.Success(Unit.Value);
    }

    private async Task<Result<Unit, string>> ConsolidateHierarchicallyAsync(List<Episode> episodes, CancellationToken ct)
    {
        // Build abstraction hierarchies from specific to general
        var hierarchyLevels = episodes
            .GroupBy(ep => ep.SuccessScore >= 0.7 ? "successful" : "failed")
            .ToList();

        _logger?.LogInformation(
            "Building hierarchical structure with {LevelCount} levels from {EpisodeCount} episodes",
            hierarchyLevels.Count,
            episodes.Count);

        foreach (var level in hierarchyLevels)
        {
            var levelEpisodes = level.ToList();
            if (levelEpisodes.Count == 0)
            {
                continue;
            }

            var isSuccess = level.Key == "successful";
            var parentGoal = isSuccess ? "successful_strategies" : "failure_patterns";

            // Summarize child goals and lessons
            var childGoals = levelEpisodes.Select(ep => ep.Goal).Distinct().ToList();
            var childLessons = levelEpisodes
                .SelectMany(ep => ep.LessonsLearned)
                .Distinct()
                .ToImmutableList();
            var childIds = levelEpisodes.Select(ep => ep.Id.ToString()).ToList();

            var summaryContent = $"{parentGoal}: {string.Join("; ", childGoals.Take(20))}";

            // Generate embedding from the summary
            var embeddingText = $"{parentGoal}\n{summaryContent}";
            var embedding = await _embeddingModel.CreateEmbeddingsAsync(embeddingText, ct);

            var parentId = Guid.NewGuid();
            var parentContext = ImmutableDictionary<string, object>.Empty
                .Add("consolidated", (object)"hierarchical")
                .Add("hierarchy_level", (object)"parent")
                .Add("child_episode_ids", (object)string.Join(",", childIds))
                .Add("child_count", (object)levelEpisodes.Count);

            // Calculate average success score for the parent
            var avgScore = levelEpisodes.Average(ep => ep.SuccessScore);

            var point = new PointStruct
            {
                Id = new PointId { Uuid = parentId.ToString() },
                Vectors = embedding,
                Payload =
                {
                    ["goal"] = parentGoal,
                    ["timestamp"] = DateTime.UtcNow.ToString("o"),
                    ["success"] = isSuccess,
                    ["success_score"] = avgScore,
                    ["duration_ms"] = 0.0,
                    ["output"] = summaryContent,
                    ["errors"] = "[]",
                    ["lessons_learned"] = JsonSerializer.Serialize(childLessons),
                    ["branch_json"] = "{}",
                    ["context"] = JsonSerializer.Serialize(parentContext),
                },
            };

            await _qdrantClient.UpsertAsync(_collectionName, new[] { point }, cancellationToken: ct);

            _logger?.LogInformation(
                "Created hierarchical parent episode {ParentId} ({ParentGoal}) summarizing {ChildCount} children with {LessonCount} lessons",
                parentId,
                parentGoal,
                levelEpisodes.Count,
                childLessons.Count);
        }

        return Result<Unit, string>.Success(Unit.Value);
    }
}
