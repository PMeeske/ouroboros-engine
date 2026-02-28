
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
    /// Forces an immediate save of all skills to disk.
    /// </summary>
    public async Task SaveSkillsAsync(CancellationToken ct = default)
    {
        await _saveLock.WaitAsync(ct);
        try
        {
            if (!_isDirty) return;

            var serializableSkills = _skills.Values.Select(ToSerializable).ToList();
            string json = JsonSerializer.Serialize(serializableSkills, JsonOptions);

            string fullPath = Path.GetFullPath(_config.StoragePath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = fullPath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, ct);
            File.Move(tempPath, fullPath, overwrite: true);
            _isDirty = false;
        }
        finally
        {
            _saveLock.Release();
        }
    }

    /// <summary>
    /// Loads skills from disk.
    /// </summary>
    public async Task LoadSkillsAsync(CancellationToken ct = default)
    {
        string fullPath = Path.GetFullPath(_config.StoragePath);
        if (!File.Exists(fullPath))
            return;

        try
        {
            string json = await File.ReadAllTextAsync(fullPath, ct);
            var serializableSkills = JsonSerializer.Deserialize<List<SerializableSkill>>(json, JsonOptions);

            if (serializableSkills != null)
            {
                foreach (var ss in serializableSkills)
                {
                    var skill = FromSerializable(ss);
                    _skills[skill.Id] = skill;

                    // Add to vector store if available
                    if (_embedding != null && _vectorStore != null)
                    {
                        await AddToVectorStoreAsync(skill, ct);
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            // Log but continue - corrupted file shouldn't crash the app
            Trace.TraceWarning("[WARN] Failed to load skills from {0}: {1}", fullPath, ex.Message);
        }
    }

    /// <summary>
    /// Deletes a skill by ID (backward compatibility).
    /// </summary>
    public async Task DeleteSkillAsync(string skillId, CancellationToken ct = default)
    {
        if (_skills.TryRemove(skillId, out _))
        {
            _isDirty = true;
            await SaveSkillsAsync(ct);
        }
    }

    /// <summary>
    /// Gets statistics about the skill registry.
    /// </summary>
    public SkillRegistryStats GetStats()
    {
        var skills = _skills.Values.ToList();
        return new SkillRegistryStats(
            TotalSkills: skills.Count,
            AverageSuccessRate: skills.Count > 0 ? skills.Average(s => s.SuccessRate) : 0,
            TotalExecutions: skills.Sum(s => s.UsageCount),
            MostUsedSkill: skills.OrderByDescending(s => s.UsageCount).FirstOrDefault()?.Name,
            MostSuccessfulSkill: skills.OrderByDescending(s => s.SuccessRate).FirstOrDefault()?.Name,
            StoragePath: Path.GetFullPath(_config.StoragePath),
            IsPersisted: File.Exists(_config.StoragePath));
    }

    private async Task AddToVectorStoreAsync(AgentSkill skill, CancellationToken ct = default)
    {
        if (_embedding == null || _vectorStore == null) return;

        try
        {
            // Create searchable text from skill
            string searchText = $"{skill.Name}: {skill.Description}. Category: {skill.Category}. Tags: {string.Join(", ", skill.Tags)}. Preconditions: {string.Join(", ", skill.Preconditions)}. Effects: {string.Join(", ", skill.Effects)}";
            float[] embedding = await _embedding.CreateEmbeddingsAsync(searchText, ct);

            var metadata = new Dictionary<string, object>
            {
                ["skill_id"] = skill.Id,
                ["skill_name"] = skill.Name,
                ["description"] = skill.Description,
                ["category"] = skill.Category,
                ["success_rate"] = skill.SuccessRate,
                ["usage_count"] = skill.UsageCount,
                ["type"] = "skill"
            };

            var vector = new Vector
            {
                Id = $"skill_{skill.Id}",
                Text = searchText,
                Embedding = embedding,
                Metadata = metadata!
            };

            await _vectorStore.AddAsync(new[] { vector }, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) // Intentional: background vector store operation
        {
            Trace.TraceWarning("[WARN] Failed to add skill '{0}' to vector store: {1}", skill.Name, ex.Message);
        }
    }

    private static SerializableSkill ToSerializable(AgentSkill skill) => new(
        skill.Id,
        skill.Name,
        skill.Description,
        skill.Category,
        skill.Preconditions.ToList(),
        skill.Effects.ToList(),
        skill.SuccessRate,
        skill.UsageCount,
        skill.AverageExecutionTime,
        skill.Tags.ToList());

    private static AgentSkill FromSerializable(SerializableSkill ss) => new(
        ss.Id,
        ss.Name,
        ss.Description,
        ss.Category,
        ss.Preconditions,
        ss.Effects,
        ss.SuccessRate,
        ss.UsageCount,
        ss.AverageExecutionTime,
        ss.Tags);

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dotProduct = 0, magnitudeA = 0, magnitudeB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        if (magnitudeA == 0 || magnitudeB == 0) return 0;
        return dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDirty)
        {
            await SaveSkillsAsync();
        }
        _saveLock.Dispose();
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
            _isDirty = true;
            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (ArgumentNullException ex)
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
        _isDirty = true;
    }
}
