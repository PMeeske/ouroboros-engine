// <copyright file="QdrantSkillRegistry.Search.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>


using System.Diagnostics;

namespace Ouroboros.Agent.MetaAI;

public sealed partial class QdrantSkillRegistry
{
    /// <summary>
    /// Finds skills matching the given criteria.
    /// </summary>
    public async Task<Result<IReadOnlyList<AgentSkill>, string>> FindSkillsAsync(
        string? category = null,
        IReadOnlyList<string>? tags = null,
        CancellationToken ct = default)
    {
        try
        {
            List<AgentSkill> allSkills = _skillsCache.Values.ToList();
            List<AgentSkill> filtered = allSkills;

            // Filter by category
            if (!string.IsNullOrWhiteSpace(category))
            {
                filtered = filtered.Where(s => s.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // Filter by tags
            if (tags != null && tags.Count > 0)
            {
                filtered = filtered.Where(s => tags.Any(t => s.Tags.Contains(t, StringComparer.OrdinalIgnoreCase))).ToList();
            }

            // Use semantic search if embedding model is available
            if (_embedding != null && (tags?.Count > 0 || !string.IsNullOrWhiteSpace(category)))
            {
                try
                {
                    string query = category ?? string.Join(" ", tags ?? Array.Empty<string>());
                    float[] queryEmbedding = await _embedding.CreateEmbeddingsAsync(query, ct).ConfigureAwait(false);

                    var collectionExists = await _client.CollectionExistsAsync(_config.CollectionName, ct).ConfigureAwait(false);
                    if (collectionExists)
                    {
                        var searchResults = await _client.SearchAsync(
                            _config.CollectionName,
                            queryEmbedding,
                            limit: 10,
                            scoreThreshold: MinimumSimilarityThreshold,
                            cancellationToken: ct).ConfigureAwait(false);

                        var matchedSkills = new List<AgentSkill>();
                        foreach (var result in searchResults)
                        {
                            if (result.Payload.TryGetValue("skill_id", out var idValue) &&
                                _skillsCache.TryGetValue(idValue.StringValue, out var skill))
                            {
                                matchedSkills.Add(skill);
                            }
                        }

                        if (matchedSkills.Count > 0)
                        {
                            filtered = matchedSkills;
                        }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Trace.TraceWarning("[WARN] Qdrant semantic search failed: {0}", ex.Message);
                    // Fall back to simple filtering
                }
            }

            // Order by success rate and usage count if no semantic matching
            if (filtered == allSkills)
            {
                filtered = filtered
                    .OrderByDescending(s => s.SuccessRate)
                    .ThenByDescending(s => s.UsageCount)
                    .ToList();
            }

            return Result<IReadOnlyList<AgentSkill>, string>.Success(filtered);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<AgentSkill>, string>.Failure($"Failed to find skills: {ex.Message}");
        }
    }

    /// <summary>
    /// Finds skills matching a goal and context.
    /// </summary>
    public async Task<List<Skill>> FindMatchingSkillsAsync(
        string goal,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default)
    {
        try
        {
            var tags = ExtractTagsFromGoal(goal);
            var result = await FindSkillsAsync(null, tags, ct).ConfigureAwait(false);
            if (!result.IsSuccess)
                return new List<Skill>();

            return result.Value.Select(s => new Skill(
                s.Name, s.Description, s.Preconditions.ToList(),
                s.Effects.Select(e => new PlanStep(e, new Dictionary<string, object>(), e, s.SuccessRate)).ToList(),
                s.SuccessRate, s.UsageCount, DateTime.UtcNow.AddDays(-s.UsageCount), DateTime.UtcNow)).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return new List<Skill>();
        }
    }

    /// <summary>
    /// Extracts a skill from an execution result.
    /// </summary>
    public Task<Result<Skill, string>> ExtractSkillAsync(
        PlanExecutionResult execution,
        string skillName,
        string description,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(execution);

            var skill = new Skill(
                Name: skillName,
                Description: description,
                Prerequisites: new List<string>(),
                Steps: execution.Plan.Steps,
                SuccessRate: execution.Success ? 1.0 : 0.0,
                UsageCount: 1,
                CreatedAt: DateTime.UtcNow,
                LastUsed: DateTime.UtcNow);

            // Also register as AgentSkill
            var agentSkill = new AgentSkill(
                Guid.NewGuid().ToString(), skill.Name, skill.Description, "learned",
                skill.Prerequisites, skill.Steps.Select(s => s.ExpectedOutcome).ToList(),
                skill.SuccessRate, skill.UsageCount,
                (long)execution.Duration.TotalMilliseconds,
                ExtractTagsFromGoal(execution.Plan.Goal));
            _skillsCache[agentSkill.Id] = agentSkill;

            return Task.FromResult(Result<Skill, string>.Success(skill));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult(Result<Skill, string>.Failure($"Failed to extract skill: {ex.Message}"));
        }
    }

    private static IReadOnlyList<string> ExtractTagsFromGoal(string goal)
    {
        if (string.IsNullOrWhiteSpace(goal))
            return Array.Empty<string>();

        var words = goal.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', ':', ';', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Distinct()
            .Take(10)
            .ToList();

        return words;
    }
}
