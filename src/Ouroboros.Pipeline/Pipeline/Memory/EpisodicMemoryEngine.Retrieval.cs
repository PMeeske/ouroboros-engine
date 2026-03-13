// <copyright file="EpisodicMemoryEngine.Retrieval.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Memory;

using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ouroboros.Domain;
using Ouroboros.Domain.States;
using Ouroboros.Domain.Vectors;
using Ouroboros.Pipeline.Verification;
using Qdrant.Client.Grpc;

public sealed partial class EpisodicMemoryEngine
{
    /// <inheritdoc/>
    public async Task<Result<ImmutableList<Episode>, string>> RetrieveSimilarEpisodesAsync(
        string query,
        int topK = 5,
        double minSimilarity = 0.7,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(query);
            if (topK <= 0)
            {
                return Result<ImmutableList<Episode>, string>.Failure("topK must be greater than 0");
            }

            if (minSimilarity < 0.0 || minSimilarity > 1.0)
            {
                return Result<ImmutableList<Episode>, string>.Failure("minSimilarity must be between 0.0 and 1.0");
            }

            // Ensure collection exists
            await EnsureCollectionInitializedAsync(ct).ConfigureAwait(false);

            // Generate query embedding
            var queryEmbedding = await _embeddingModel.CreateEmbeddingsAsync(query, ct).ConfigureAwait(false);

            // Search in Qdrant
            var searchResult = await _qdrantClient.SearchAsync(
                _collectionName,
                queryEmbedding,
                limit: (ulong)topK,
                scoreThreshold: (float)minSimilarity,
                cancellationToken: ct).ConfigureAwait(false);

            // Convert results to Episode objects
            var episodes = searchResult
                .Select(point => DeserializeEpisode(point))
                .Where(ep => ep.HasValue)
                .Select(ep => ep.Value!)
                .ToImmutableList();

            stopwatch.Stop();
            _logger?.LogInformation(
                "Retrieved {Count} episodes in {ElapsedMs}ms for query: {Query}",
                episodes.Count,
                stopwatch.ElapsedMilliseconds,
                query);

            return Result<ImmutableList<Episode>, string>.Success(episodes);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Failed to retrieve similar episodes");
            return Result<ImmutableList<Episode>, string>.Failure($"Failed to retrieve episodes: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<Plan, string>> PlanWithExperienceAsync(
        string goal,
        ImmutableList<Episode> relevantEpisodes,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(goal);
            ArgumentNullException.ThrowIfNull(relevantEpisodes);

            _logger?.LogInformation(
                "Generating plan for goal '{Goal}' using {EpisodeCount} relevant episodes",
                goal,
                relevantEpisodes.Count);

            // Extract successful patterns from episodes
            var successfulPatterns = relevantEpisodes
                .Where(ep => ep.SuccessScore > 0.7)
                .SelectMany(ep => ep.LessonsLearned)
                .Distinct()
                .ToList();

            // Extract failed patterns to avoid
            var failedPatterns = relevantEpisodes
                .Where(ep => ep.SuccessScore < 0.3)
                .SelectMany(ep => ep.Result.Errors)
                .Distinct()
                .ToList();

            // Build plan description incorporating learned patterns
            var planDescription = BuildPlanDescription(goal, successfulPatterns, failedPatterns);

            // Generate plan actions based on successful episodes
            var actions = GeneratePlanActions(goal, relevantEpisodes);

            var plan = new Plan(planDescription, actions);

            _logger?.LogInformation("Generated plan with {ActionCount} actions", actions.Count);

            return Result<Plan, string>.Success(plan);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Failed to generate plan with experience");
            return Result<Plan, string>.Failure($"Failed to generate plan: {ex.Message}");
        }
    }

    private Option<Episode> DeserializeEpisode(ScoredPoint point)
    {
        return DeserializeEpisodeCore(point.Id, point.Payload, point.Vectors.Vector);
    }

    private Option<Episode> DeserializeEpisodeFromRetrieved(RetrievedPoint point)
    {
        return DeserializeEpisodeCore(point.Id, point.Payload, point.Vectors.Vector);
    }

    private Option<Episode> DeserializeEpisodeCore(PointId id, Google.Protobuf.Collections.MapField<string, Value> payload, VectorOutput vector)
    {
        try
        {
            var episodeId = Guid.Parse(id.Uuid);
            var timestamp = DateTime.Parse(payload["timestamp"].StringValue);
            var goal = payload["goal"].StringValue;
            var success = payload["success"].BoolValue;
            var successScore = payload["success_score"].DoubleValue;
            var durationMs = payload["duration_ms"].DoubleValue;
            var output = payload["output"].StringValue;
            var errorsJson = payload["errors"].StringValue;
            var lessonsJson = payload["lessons_learned"].StringValue;
            _ = payload["branch_json"].StringValue; // S1481: read but unused; kept for forward-compat deserialization
            var contextJson = payload["context"].StringValue;

            var errors = JsonSerializer.Deserialize<ImmutableList<string>>(errorsJson) ?? ImmutableList<string>.Empty;
            var lessonsLearned = JsonSerializer.Deserialize<ImmutableList<string>>(lessonsJson) ?? ImmutableList<string>.Empty;
            var context = JsonSerializer.Deserialize<ImmutableDictionary<string, object>>(contextJson) ?? ImmutableDictionary<string, object>.Empty;

            var outcome = new Outcome(success, output, TimeSpan.FromMilliseconds(durationMs), errors);

            // Note: We're not deserializing the full PipelineBranch here for performance
            // It can be reconstructed from the JSON when needed
            var dataSource = LangChain.DocumentLoaders.DataSource.FromPath(Environment.CurrentDirectory);
            var emptyBranch = new PipelineBranch("deserialized", new TrackedVectorStore(), dataSource);

            // For deserialized episodes, embedding is not critical since already stored in Qdrant
            // Use empty embedding to save processing time
            var embedding = new float[768]; // Standard embedding size

            var episode = new Episode(
                episodeId,
                timestamp,
                goal,
                emptyBranch,
                outcome,
                successScore,
                lessonsLearned,
                context,
                embedding);

            return Option<Episode>.Some(episode);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Failed to deserialize episode from point");
            return Option<Episode>.None();
        }
    }

    private static string BuildPlanDescription(string goal, List<string> successPatterns, List<string> failurePatterns)
    {
        var description = $"Plan for: {goal}\n\n";

        if (successPatterns.Count > 0)
        {
            description += "Successful patterns to follow:\n";
            description += string.Join("\n", successPatterns.Take(3).Select(p => $"- {p}"));
            description += "\n\n";
        }

        if (failurePatterns.Count > 0)
        {
            description += "Patterns to avoid:\n";
            description += string.Join("\n", failurePatterns.Take(3).Select(p => $"- {p}"));
        }

        return description;
    }

    private static ImmutableList<PlanAction> GeneratePlanActions(string goal, ImmutableList<Episode> episodes)
    {
        var actions = new List<PlanAction>();

        // Extract common tool usage patterns from successful episodes
        var successfulEpisodes = episodes.Where(ep => ep.SuccessScore > 0.7).ToList();

        if (successfulEpisodes.Count > 0)
        {
            // Add generic actions based on the goal
            actions.Add(new ToolAction("analysis", goal));
            actions.Add(new ToolAction("execution", goal));
            actions.Add(new ToolAction("validation", "verify results"));
        }
        else
        {
            // Fallback to basic actions if no successful episodes
            actions.Add(new ToolAction("reasoning", goal));
        }

        return actions.ToImmutableList();
    }
}
