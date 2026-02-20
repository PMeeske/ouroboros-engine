// <copyright file="QdrantSkillRegistry.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Qdrant-backed Skill Registry Implementation
// Stores skills in Qdrant vector database for persistence
// and semantic search capabilities
// ==========================================================

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Ouroboros.Abstractions;
using Match = Qdrant.Client.Grpc.Match;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Qdrant-backed implementation of skill registry.
/// Stores skills directly in Qdrant for persistence and semantic search.
/// </summary>
public sealed class QdrantSkillRegistry : ISkillRegistry, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, AgentSkill> _skillsCache = new();
    private readonly IEmbeddingModel? _embedding;
    private readonly QdrantClient _client;
    private readonly QdrantSkillConfig _config;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private bool _isInitialized;
    private readonly bool _disposeClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public QdrantSkillRegistry(
        IEmbeddingModel? embedding = null,
        QdrantSkillConfig? config = null)
    {
        _embedding = embedding;
        _config = config ?? new QdrantSkillConfig();

        // Parse connection string to extract host and port
        var normalizedConnectionString = NormalizeConnectionString(_config.ConnectionString);
        var uri = new Uri(normalizedConnectionString);
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 6334; // Default to gRPC port
        var useHttps = uri.Scheme == "https";

        _client = new QdrantClient(host, port, useHttps);
        _disposeClient = true;
    }

    public QdrantSkillRegistry(
        QdrantClient client,
        IEmbeddingModel? embedding = null,
        QdrantSkillConfig? config = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _embedding = embedding;
        _config = config ?? new QdrantSkillConfig();
        _disposeClient = false;
    }

    /// <summary>
    /// Initializes the registry by loading persisted skills from Qdrant.
    /// Call this before using the registry.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized) return;

        await EnsureCollectionExistsAsync(ct);
        await LoadSkillsFromQdrantAsync(ct);
        _isInitialized = true;
    }

    /// <summary>
    /// Registers a new skill and persists it to Qdrant.
    /// </summary>
    public async Task<Result<Unit, string>> RegisterSkillAsync(AgentSkill skill, CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(skill);
            _skillsCache[skill.Id] = skill;
            await SaveSkillToQdrantAsync(skill, ct);
            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to register skill: {ex.Message}");
        }
    }

    /// <summary>
    /// Minimum similarity score threshold for semantic skill matching.
    /// Skills with similarity below this value are considered unrelated.
    /// Qdrant scores typically range from 0 to 1 for cosine similarity.
    /// A threshold of 0.75+ ensures strong semantic match, not just topical relatedness.
    /// </summary>
    private const float MinimumSimilarityThreshold = 0.75f;

    /// <summary>
    /// Gets a skill by identifier.
    /// </summary>
    public Task<Result<AgentSkill, string>> GetSkillAsync(string skillId, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(skillId))
                return Task.FromResult(Result<AgentSkill, string>.Failure("Skill ID cannot be empty"));

            if (_skillsCache.TryGetValue(skillId, out AgentSkill? skill))
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
                    float[] queryEmbedding = await _embedding.CreateEmbeddingsAsync(query, ct);

                    var collectionExists = await _client.CollectionExistsAsync(_config.CollectionName, ct);
                    if (collectionExists)
                    {
                        var searchResults = await _client.SearchAsync(
                            _config.CollectionName,
                            queryEmbedding,
                            limit: 10,
                            scoreThreshold: MinimumSimilarityThreshold,
                            cancellationToken: ct);

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
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[WARN] Qdrant semantic search failed: {ex.Message}");
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
            
            if (!_skillsCache.ContainsKey(skill.Id))
                return Result<Unit, string>.Failure($"Skill '{skill.Id}' not found");

            _skillsCache[skill.Id] = skill;
            await SaveSkillToQdrantAsync(skill, ct);

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

            if (!_skillsCache.TryGetValue(skillId, out var existing))
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

            _skillsCache[skillId] = updated;
            
            if (_config.AutoSave)
            {
                await SaveSkillToQdrantAsync(updated, ct);
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

            if (!_skillsCache.TryRemove(skillId, out _))
                return Result<Unit, string>.Failure($"Skill '{skillId}' not found");

            // Remove from Qdrant
            try
            {
                var collectionExists = await _client.CollectionExistsAsync(_config.CollectionName, ct);
                if (collectionExists)
                {
                    await _client.DeleteAsync(
                        _config.CollectionName,
                        new Filter
                        {
                            Must =
                            {
                                new Condition
                                {
                                    Field = new FieldCondition
                                    {
                                        Key = "skill_id",
                                        Match = new Match { Keyword = skillId }
                                    }
                                }
                            }
                        },
                        cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[WARN] Failed to delete skill '{skillId}' from Qdrant: {ex.Message}");
            }

            return Result<Unit, string>.Success(Unit.Value);
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
            var skills = _skillsCache.Values.OrderByDescending(s => s.SuccessRate).ToList();
            return Task.FromResult(Result<IReadOnlyList<AgentSkill>, string>.Success((IReadOnlyList<AgentSkill>)skills));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<IReadOnlyList<AgentSkill>, string>.Failure($"Failed to get all skills: {ex.Message}"));
        }
    }

    /// <summary>
    /// Gets statistics about the skill registry.
    /// </summary>
    public QdrantSkillRegistryStats GetStats()
    {
        var skills = _skillsCache.Values.ToList();
        return new QdrantSkillRegistryStats(
            TotalSkills: skills.Count,
            AverageSuccessRate: skills.Count > 0 ? skills.Average(s => s.SuccessRate) : 0,
            TotalExecutions: skills.Sum(s => s.UsageCount),
            MostUsedSkill: skills.OrderByDescending(s => s.UsageCount).FirstOrDefault()?.Name,
            MostSuccessfulSkill: skills.OrderByDescending(s => s.SuccessRate).FirstOrDefault()?.Name,
            ConnectionString: _config.ConnectionString,
            CollectionName: _config.CollectionName,
            IsConnected: _isInitialized);
    }

    private int? _detectedVectorSize;

    private async Task EnsureCollectionExistsAsync(CancellationToken ct = default)
    {
        try
        {
            var collectionExists = await _client.CollectionExistsAsync(_config.CollectionName, ct);
            if (!collectionExists)
            {
                // Detect vector size from embedding model if available
                int vectorSize = _config.VectorSize;
                if (_embedding != null)
                {
                    try
                    {
                        var sampleEmbedding = await _embedding.CreateEmbeddingsAsync("sample text for dimension detection", ct);
                        vectorSize = sampleEmbedding.Length;
                        _detectedVectorSize = vectorSize;
                        Console.WriteLine($"[qdrant] Detected embedding dimension: {vectorSize}");
                    }
                    catch
                    {
                        // Use default if detection fails
                    }
                }

                await _client.CreateCollectionAsync(
                    _config.CollectionName,
                    new VectorParams
                    {
                        Size = (ulong)vectorSize,
                        Distance = Distance.Cosine
                    },
                    cancellationToken: ct);

                Console.WriteLine($"[qdrant] Created skills collection: {_config.CollectionName}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to ensure Qdrant collection: {ex.Message}");
        }
    }

    private async Task SaveSkillToQdrantAsync(AgentSkill skill, CancellationToken ct = default)
    {
        await _syncLock.WaitAsync(ct);
        try
        {
            // Create searchable text from skill
            string searchText = $"{skill.Name}: {skill.Description}. Category: {skill.Category}. Tags: {string.Join(", ", skill.Tags)}. Preconditions: {string.Join(", ", skill.Preconditions)}. Effects: {string.Join(", ", skill.Effects)}";

            // Generate embedding
            float[] embedding;
            if (_embedding != null)
            {
                embedding = await _embedding.CreateEmbeddingsAsync(searchText, ct);
            }
            else
            {
                // Use a simple hash-based "embedding" as fallback (not for semantic search)
                int vectorSize = _detectedVectorSize ?? _config.VectorSize;
                embedding = GenerateFallbackEmbedding(searchText, vectorSize);
            }

            // Serialize skill data for storage
            var skillJson = JsonSerializer.Serialize(new SerializableSkillData(
                skill.Id,
                skill.Name,
                skill.Description,
                skill.Category,
                skill.Preconditions.ToList(),
                skill.Effects.ToList(),
                skill.SuccessRate,
                skill.UsageCount,
                skill.AverageExecutionTime,
                skill.Tags.ToList()
            ), JsonOptions);

            var point = new PointStruct
            {
                Id = new PointId { Uuid = GenerateSkillId(skill.Id) },
                Vectors = embedding,
                Payload =
                {
                    ["skill_id"] = skill.Id,
                    ["skill_name"] = skill.Name,
                    ["description"] = skill.Description,
                    ["category"] = skill.Category,
                    ["success_rate"] = skill.SuccessRate,
                    ["usage_count"] = skill.UsageCount,
                    ["average_execution_time"] = skill.AverageExecutionTime,
                    ["skill_data"] = skillJson,
                    ["type"] = "skill"
                }
            };

            await _client.UpsertAsync(_config.CollectionName, new[] { point }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to save skill '{skill.Name}' to Qdrant: {ex.Message}");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task LoadSkillsFromQdrantAsync(CancellationToken ct = default)
    {
        try
        {
            var collectionExists = await _client.CollectionExistsAsync(_config.CollectionName, ct);
            if (!collectionExists)
            {
                return;
            }

            // Scroll through all points in the collection using the proper API
            var scrollResult = await _client.ScrollAsync(
                _config.CollectionName,
                payloadSelector: true,
                limit: 1000,
                cancellationToken: ct);

            foreach (var point in scrollResult.Result)
            {
                try
                {
                    if (point.Payload.TryGetValue("skill_data", out var skillDataValue) &&
                        !string.IsNullOrWhiteSpace(skillDataValue.StringValue))
                    {
                        var skillData = JsonSerializer.Deserialize<SerializableSkillData>(
                            skillDataValue.StringValue, JsonOptions);

                        if (skillData != null)
                        {
                            var skillId = string.IsNullOrWhiteSpace(skillData.Id)
                                ? null
                                : skillData.Id.Trim();
                            if (string.IsNullOrWhiteSpace(skillId))
                            {
                                Console.Error.WriteLine("[WARN] Skipping Qdrant skill with missing id");
                                continue;
                            }

                            var skillName = string.IsNullOrWhiteSpace(skillData.Name)
                                ? skillId
                                : skillData.Name.Trim();
                            var description = skillData.Description?.Trim() ?? string.Empty;
                            var category = string.IsNullOrWhiteSpace(skillData.Category)
                                ? "general"
                                : skillData.Category.Trim();

                            var preconditions = skillData.Preconditions?
                                .Where(v => !string.IsNullOrWhiteSpace(v))
                                .Select(v => v.Trim())
                                .ToList()
                                ?? new List<string>();

                            var effects = skillData.Effects?
                                .Where(v => !string.IsNullOrWhiteSpace(v))
                                .Select(v => v.Trim())
                                .ToList()
                                ?? new List<string>();

                            var tags = skillData.Tags?
                                .Where(v => !string.IsNullOrWhiteSpace(v))
                                .Select(v => v.Trim())
                                .ToList()
                                ?? new List<string>();

                            var skill = new AgentSkill(
                                skillId,
                                skillName,
                                description,
                                category,
                                preconditions,
                                effects,
                                skillData.SuccessRate,
                                skillData.UsageCount,
                                skillData.AverageExecutionTime,
                                tags);

                            _skillsCache[skill.Id] = skill;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[WARN] Failed to deserialize skill from Qdrant: {ex.Message}");
                }
            }

            if (_skillsCache.Count > 0)
            {
                Console.WriteLine($"[qdrant] Loaded {_skillsCache.Count} skills from {_config.CollectionName}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to load skills from Qdrant: {ex.Message}");
        }
    }

    private static string GenerateSkillId(string skillName)
    {
        // Create a deterministic UUID from the skill name
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes($"skill_{skillName}"));
        return new Guid(hash).ToString();
    }

    private static string NormalizeConnectionString(string? rawConnectionString)
    {
        var endpoint = (rawConnectionString ?? string.Empty).Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return "http://localhost:6334";
        }

        var schemeSeparatorCount = endpoint.Split("://", StringSplitOptions.None).Length - 1;
        if (schemeSeparatorCount > 1)
        {
            return "http://localhost:6334";
        }

        if (!endpoint.Contains("://", StringComparison.Ordinal))
        {
            endpoint = $"http://{endpoint}";
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return "http://localhost:6334";
        }

        if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return "http://localhost:6334";
        }

        if (string.IsNullOrWhiteSpace(uri.Host) || uri.Host.Contains("://", StringComparison.Ordinal))
        {
            return "http://localhost:6334";
        }

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private static float[] GenerateFallbackEmbedding(string text, int size)
    {
        // Generate a simple hash-based vector as fallback when no embedding model is available
        // This is NOT for semantic search - just for storage
        var embedding = new float[size];
        var hash = text.GetHashCode();
        var random = new Random(hash);
        for (int i = 0; i < size; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1); // -1 to 1
        }
        // Normalize
        var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
        if (magnitude > 0)
        {
            for (int i = 0; i < size; i++)
                embedding[i] /= magnitude;
        }
        return embedding;
    }

    public async ValueTask DisposeAsync()
    {
        _syncLock.Dispose();
        if (_disposeClient)
        {
            _client.Dispose();
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Registers a skill (sync convenience method).
    /// </summary>
    public Result<Unit, string> RegisterSkill(AgentSkill skill)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(skill);
            _skillsCache[skill.Id] = skill;
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

        return _skillsCache.TryGetValue(skillId, out AgentSkill? skill) ? skill : null;
    }

    /// <summary>
    /// Gets all registered skills (sync convenience method).
    /// </summary>
    public IReadOnlyList<AgentSkill> GetAllSkills()
    {
        return _skillsCache.Values.OrderByDescending(s => s.SuccessRate).ToList();
    }

    /// <summary>
    /// Records a skill execution (sync convenience method).
    /// </summary>
    public void RecordSkillExecution(string skillId, bool success, long executionTimeMs)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            return;

        if (!_skillsCache.TryGetValue(skillId, out var existing))
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

        _skillsCache[skillId] = updated;
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
            _skillsCache[agentSkill.Id] = agentSkill;

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