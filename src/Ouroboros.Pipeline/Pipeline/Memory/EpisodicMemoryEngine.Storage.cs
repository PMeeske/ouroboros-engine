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

        try
        {
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
            if (group.Count() > 1)
            {
                // Could implement: merge similar episodes into a single compressed representation
                _logger?.LogDebug("Group '{Goal}' has {Count} episodes to compress", group.Key, group.Count());
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

        // Patterns could be stored as high-level abstract episodes
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

        // Could implement: create abstract parent episodes that summarize children
        return Result<Unit, string>.Success(Unit.Value);
    }
}
