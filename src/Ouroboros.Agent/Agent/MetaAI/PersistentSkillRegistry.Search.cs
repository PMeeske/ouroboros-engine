#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using LangChain.Databases;
using Ouroboros.Abstractions;

namespace Ouroboros.Agent.MetaAI;

public sealed partial class PersistentSkillRegistry
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
            List<AgentSkill> allSkills = _skills.Values.ToList();
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

            // Use vector store for semantic search if available
            if (_embedding != null && _vectorStore != null && _vectorStore.GetAll().Any() && (tags?.Count > 0 || !string.IsNullOrWhiteSpace(category)))
            {
                try
                {
                    string query = category ?? string.Join(" ", tags ?? Array.Empty<string>());
                    float[] queryEmbedding = await _embedding.CreateEmbeddingsAsync(query, ct);
                    var similarDocs = await _vectorStore.GetSimilarDocumentsAsync(queryEmbedding, Math.Min(10, filtered.Count));

                    var matchedSkills = new List<AgentSkill>();
                    foreach (var doc in similarDocs)
                    {
                        if (doc.Metadata?.TryGetValue("skill_id", out var idObj) == true &&
                            idObj is string id &&
                            _skills.TryGetValue(id, out var skill))
                        {
                            matchedSkills.Add(skill);
                        }
                    }

                    if (matchedSkills.Count > 0)
                    {
                        var scoredSkills = new List<(AgentSkill skill, double score)>();
                        foreach (var skill in matchedSkills)
                        {
                            float[] skillEmbedding = await _embedding.CreateEmbeddingsAsync(skill.Description, ct);
                            double similarity = CosineSimilarity(queryEmbedding, skillEmbedding);
                            if (similarity >= MinimumSimilarityThreshold)
                            {
                                scoredSkills.Add((skill, similarity));
                            }
                        }

                        if (scoredSkills.Count > 0)
                        {
                            filtered = scoredSkills
                                .OrderByDescending(x => x.score)
                                .Select(x => x.skill)
                                .ToList();
                        }
                    }
                }
                catch
                {
                    // Fall back to simple filtering
                }
            }

            // Use embedding-based similarity if available and no vector store
            if (_embedding != null && (tags?.Count > 0 || !string.IsNullOrWhiteSpace(category)))
            {
                try
                {
                    string query = category ?? string.Join(" ", tags ?? Array.Empty<string>());
                    float[] queryEmbedding = await _embedding.CreateEmbeddingsAsync(query, ct);

                    var skillScores = new List<(AgentSkill skill, double score)>();
                    foreach (var skill in filtered)
                    {
                        float[] skillEmbedding = await _embedding.CreateEmbeddingsAsync(skill.Description, ct);
                        double similarity = CosineSimilarity(queryEmbedding, skillEmbedding);
                        skillScores.Add((skill, similarity));
                    }

                    filtered = skillScores
                        .Where(x => x.score >= MinimumSimilarityThreshold)
                        .OrderByDescending(x => x.score)
                        .Select(x => x.skill)
                        .ToList();
                }
                catch
                {
                    // Fall back to simple ordering
                }
            }

            // Order by success rate and usage count if no semantic matching applied
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
        catch (Exception ex) // Intentional: embedding + vector store operations across providers
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
            var result = await FindSkillsAsync(null, tags, ct);
            if (!result.IsSuccess)
                return new List<Skill>();

            return result.Value.Select(s => new Skill(
                s.Name, s.Description, s.Preconditions.ToList(),
                s.Effects.Select(e => new PlanStep(e, new Dictionary<string, object>(), e, s.SuccessRate)).ToList(),
                s.SuccessRate, s.UsageCount, DateTime.UtcNow.AddDays(-s.UsageCount), DateTime.UtcNow)).ToList();
        }
        catch
        {
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
            _skills[agentSkill.Id] = agentSkill;
            _isDirty = true;

            return Task.FromResult(Result<Skill, string>.Success(skill));
        }
        catch (ArgumentNullException ex)
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
