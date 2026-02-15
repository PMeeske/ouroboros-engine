#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Skill Registry Implementation
// Manages learned skills and pattern reuse
// ==========================================================

using System.Collections.Concurrent;
using Ouroboros.Abstractions;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Implementation of skill registry for learning and reusing successful patterns.
/// </summary>
public sealed class SkillRegistry : ISkillRegistry
{
    private readonly ConcurrentDictionary<string, AgentSkill> _skills = new();
    private readonly IEmbeddingModel? _embedding;

    public SkillRegistry(IEmbeddingModel? embedding = null)
    {
        _embedding = embedding;
    }

    /// <summary>
    /// Registers a new skill.
    /// </summary>
    public Task<Result<Unit, string>> RegisterSkillAsync(AgentSkill skill, CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(skill);
            _skills[skill.Id] = skill;
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<Unit, string>.Failure($"Failed to register skill: {ex.Message}"));
        }
    }

    /// <summary>
    /// Gets a skill by identifier.
    /// </summary>
    public Task<Result<AgentSkill, string>> GetSkillAsync(string skillId, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return Task.FromResult(Result<AgentSkill, string>.Failure("Skill ID cannot be empty"));

            if (_skills.TryGetValue(skillId, out AgentSkill? skill))
                return Task.FromResult(Result<AgentSkill, string>.Success(skill));

            return Task.FromResult(Result<AgentSkill, string>.Failure($"Skill '{skillId}' not found"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<AgentSkill, string>.Failure($"Failed to get skill: {ex.Message}"));
        }
    }

    /// <summary>
    /// Minimum similarity threshold for semantic skill matching.
    /// Skills with similarity below this value are considered unrelated.
    /// </summary>
    private const double MinimumSimilarityThreshold = 0.75;

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

            // Use semantic similarity if embedding model available and we have tags/category as query
            if (_embedding != null && (tags?.Count > 0 || !string.IsNullOrWhiteSpace(category)))
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
            else
            {
                // Order by success rate and usage count
                filtered = filtered
                    .OrderByDescending(s => s.SuccessRate)
                    .ThenByDescending(s => s.UsageCount)
                    .ToList();
            }

            return Result<IReadOnlyList<AgentSkill>, string>.Success(filtered);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<AgentSkill>, string>.Failure($"Failed to find skills: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates an existing skill's information.
    /// </summary>
    public Task<Result<Unit, string>> UpdateSkillAsync(AgentSkill skill, CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(skill);
            
            if (!_skills.ContainsKey(skill.Id))
                return Task.FromResult(Result<Unit, string>.Failure($"Skill '{skill.Id}' not found"));

            _skills[skill.Id] = skill;
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<Unit, string>.Failure($"Failed to update skill: {ex.Message}"));
        }
    }

    /// <summary>
    /// Records a skill execution result to update statistics.
    /// </summary>
    public Task<Result<Unit, string>> RecordExecutionAsync(
        string skillId,
        bool success,
        long executionTimeMs,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return Task.FromResult(Result<Unit, string>.Failure("Skill ID cannot be empty"));

            if (!_skills.TryGetValue(skillId, out var existing))
                return Task.FromResult(Result<Unit, string>.Failure($"Skill '{skillId}' not found"));

            int newCount = existing.UsageCount + 1;
            double newSuccessRate = ((existing.SuccessRate * existing.UsageCount) + (success ? 1.0 : 0.0)) / newCount;
            long newAvgTime = ((existing.AverageExecutionTime * existing.UsageCount) + executionTimeMs) / newCount;

            var updated = existing with
            {
                UsageCount = newCount,
                SuccessRate = newSuccessRate,
                AverageExecutionTime = newAvgTime
            };

            _skills[skillId] = updated;
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<Unit, string>.Failure($"Failed to record execution: {ex.Message}"));
        }
    }

    /// <summary>
    /// Removes a skill from the registry.
    /// </summary>
    public Task<Result<Unit, string>> UnregisterSkillAsync(string skillId, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return Task.FromResult(Result<Unit, string>.Failure("Skill ID cannot be empty"));

            if (_skills.TryRemove(skillId, out _))
                return Task.FromResult(Result<Unit, string>.Success(Unit.Value));

            return Task.FromResult(Result<Unit, string>.Failure($"Skill '{skillId}' not found"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<Unit, string>.Failure($"Failed to unregister skill: {ex.Message}"));
        }
    }

    /// <summary>
    /// Gets all registered skills.
    /// </summary>
    public Task<Result<IReadOnlyList<AgentSkill>, string>> GetAllSkillsAsync(CancellationToken ct = default)
    {
        try
        {
            var skills = _skills.Values.OrderByDescending(s => s.SuccessRate).ToList();
            return Task.FromResult(Result<IReadOnlyList<AgentSkill>, string>.Success((IReadOnlyList<AgentSkill>)skills));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<IReadOnlyList<AgentSkill>, string>.Failure($"Failed to get all skills: {ex.Message}"));
        }
    }

    /// <summary>
    /// Registers a skill (sync convenience method).
    /// </summary>
    public Result<Unit, string> RegisterSkill(AgentSkill skill)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(skill);
            _skills[skill.Id] = skill;
            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to register skill: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a skill by identifier (sync convenience method).
    /// </summary>
    public AgentSkill? GetSkill(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            return null;

        return _skills.TryGetValue(skillId, out AgentSkill? skill) ? skill : null;
    }

    /// <summary>
    /// Gets all registered skills (sync convenience method).
    /// </summary>
    public IReadOnlyList<AgentSkill> GetAllSkills()
    {
        return _skills.Values.OrderByDescending(s => s.SuccessRate).ToList();
    }

    /// <summary>
    /// Records a skill execution (sync convenience method).
    /// </summary>
    public void RecordSkillExecution(string skillId, bool success, long executionTimeMs)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            return;

        if (!_skills.TryGetValue(skillId, out var existing))
            return;

        int newCount = existing.UsageCount + 1;
        double newSuccessRate = ((existing.SuccessRate * existing.UsageCount) + (success ? 1.0 : 0.0)) / newCount;
        long newAvgTime = ((existing.AverageExecutionTime * existing.UsageCount) + executionTimeMs) / newCount;

        var updated = existing with
        {
            UsageCount = newCount,
            SuccessRate = newSuccessRate,
            AverageExecutionTime = newAvgTime
        };

        _skills[skillId] = updated;
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
            // Extract tags from goal
            var tags = ExtractTagsFromGoal(goal);
            
            var result = await FindSkillsAsync(null, tags, ct);
            if (!result.IsSuccess)
                return new List<Skill>();

            // Convert AgentSkill to Skill
            return result.Value.Select(s => new Skill(
                s.Id, s.Name, s.Description, s.Category,
                s.Preconditions, s.Effects, s.SuccessRate,
                s.UsageCount, s.AverageExecutionTime, s.Tags)).ToList();
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

            // Create skill from execution
            var skill = new Skill(
                Id: Guid.NewGuid().ToString(),
                Name: skillName,
                Description: description,
                Category: "learned",
                Preconditions: new List<string>(),
                Effects: execution.Plan.Steps.Select(s => s.ExpectedOutcome).ToList(),
                SuccessRate: execution.Success ? 1.0 : 0.0,
                UsageCount: 1,
                AverageExecutionTime: (long)execution.Duration.TotalMilliseconds,
                Tags: ExtractTagsFromGoal(execution.Plan.Goal));

            // Also register as AgentSkill
            var agentSkill = new AgentSkill(
                skill.Id, skill.Name, skill.Description, skill.Category,
                skill.Preconditions, skill.Effects, skill.SuccessRate,
                skill.UsageCount, skill.AverageExecutionTime, skill.Tags);
            _skills[agentSkill.Id] = agentSkill;

            return Task.FromResult(Result<Skill, string>.Success(skill));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<Skill, string>.Failure($"Failed to extract skill: {ex.Message}"));
        }
    }

    private static IReadOnlyList<string> ExtractTagsFromGoal(string goal)
    {
        if (string.IsNullOrWhiteSpace(goal))
            return Array.Empty<string>();

        // Simple tag extraction - split by common delimiters and filter
        var words = goal.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', ':', ';', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Distinct()
            .Take(10)
            .ToList();

        return words;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0;

        double dotProduct = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;

        return dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
    }
}
