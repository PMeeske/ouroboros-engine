#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Persistent Skill Registry Implementation
// Stores skills to disk/vector store for persistence across restarts
// ==========================================================

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using LangChain.Databases;
using Ouroboros.Abstractions;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Persistent implementation of skill registry that saves skills to disk and optionally to a vector store.
/// Enables skill persistence across application restarts and semantic skill search via embeddings.
/// </summary>
public sealed class PersistentSkillRegistry : ISkillRegistry, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, AgentSkill> _skills = new();
    private readonly IEmbeddingModel? _embedding;
    private readonly TrackedVectorStore? _vectorStore;
    private readonly PersistentSkillConfig _config;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private bool _isDirty;
    private bool _isInitialized;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public PersistentSkillRegistry(
        IEmbeddingModel? embedding = null,
        TrackedVectorStore? vectorStore = null,
        PersistentSkillConfig? config = null)
    {
        _embedding = embedding;
        _vectorStore = vectorStore;
        _config = config ?? new PersistentSkillConfig();
    }

    /// <summary>
    /// Initializes the registry by loading persisted skills.
    /// Call this before using the registry.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized) return;

        await LoadSkillsAsync(ct);
        _isInitialized = true;
    }

    /// <summary>
    /// Registers a new skill and persists it.
    /// </summary>
    public async Task<Result<Unit, string>> RegisterSkillAsync(AgentSkill skill, CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(skill);
            _skills[skill.Id] = skill;
            _isDirty = true;

            await SaveSkillsAsync(ct);

            if (_embedding != null && _vectorStore != null)
            {
                await AddToVectorStoreAsync(skill, ct);
            }

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to register skill: {ex.Message}");
        }
    }

    /// <summary>
    /// Minimum similarity threshold for semantic skill matching.
    /// Skills with similarity below this value are considered unrelated.
    /// </summary>
    private const double MinimumSimilarityThreshold = 0.75;

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
        catch (Exception ex)
        {
            return Result<IReadOnlyList<AgentSkill>, string>.Failure($"Failed to find skills: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates an existing skill's information.
    /// </summary>
    public async Task<Result<Unit, string>> UpdateSkillAsync(AgentSkill skill, CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(skill);
            
            if (!_skills.ContainsKey(skill.Id))
                return Result<Unit, string>.Failure($"Skill '{skill.Id}' not found");

            _skills[skill.Id] = skill;
            _isDirty = true;
            await SaveSkillsAsync(ct);

            // Update in vector store if available
            if (_embedding != null && _vectorStore != null)
            {
                await AddToVectorStoreAsync(skill, ct);
            }

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to update skill: {ex.Message}");
        }
    }

    /// <summary>
    /// Records a skill execution result to update statistics.
    /// </summary>
    public async Task<Result<Unit, string>> RecordExecutionAsync(
        string skillId,
        bool success,
        long executionTimeMs,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return Result<Unit, string>.Failure("Skill ID cannot be empty");

            if (!_skills.TryGetValue(skillId, out var existing))
                return Result<Unit, string>.Failure($"Skill '{skillId}' not found");

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
            
            if (_config.AutoSave)
            {
                await SaveSkillsAsync(ct);
            }

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to record execution: {ex.Message}");
        }
    }

    /// <summary>
    /// Removes a skill from the registry.
    /// </summary>
    public async Task<Result<Unit, string>> UnregisterSkillAsync(string skillId, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return Result<Unit, string>.Failure("Skill ID cannot be empty");

            if (_skills.TryRemove(skillId, out _))
            {
                _isDirty = true;
                await SaveSkillsAsync(ct);
                return Result<Unit, string>.Success(Unit.Value);
            }

            return Result<Unit, string>.Failure($"Skill '{skillId}' not found");
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to unregister skill: {ex.Message}");
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
    /// Forces an immediate save of all skills to disk.
    /// </summary>
    public async Task SaveSkillsAsync(CancellationToken ct = default)
    {
        if (!_isDirty) return;

        await _saveLock.WaitAsync(ct);
        try
        {
            var serializableSkills = _skills.Values.Select(ToSerializable).ToList();
            string json = JsonSerializer.Serialize(serializableSkills, JsonOptions);

            string fullPath = Path.GetFullPath(_config.StoragePath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, json, ct);
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
            Console.Error.WriteLine($"[WARN] Failed to load skills from {fullPath}: {ex.Message}");
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
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to add skill '{skill.Name}' to vector store: {ex.Message}");
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
        _isDirty = true;
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
        catch (Exception ex)
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