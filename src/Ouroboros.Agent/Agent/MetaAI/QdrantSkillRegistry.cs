// <copyright file="QdrantSkillRegistry.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

// ==========================================================
// Qdrant-backed Skill Registry Implementation
// Stores skills in Qdrant vector database for persistence
// and semantic search capabilities
// ==========================================================

using System.Text.Json.Serialization;
using Ouroboros.Core.Configuration;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Ouroboros.Abstractions;
using Match = Qdrant.Client.Grpc.Match;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Qdrant-backed implementation of skill registry.
/// Stores skills directly in Qdrant for persistence and semantic search.
/// </summary>
/// <remarks>
/// Direct Qdrant.Client usage with typed skill payloads, collection-existence checks,
/// and filter-based deletion. Migrate upsert/search paths to IVectorStoreRecordCollection
/// when SK typed record support covers the skill payload schema.
/// </remarks>
[Obsolete("Use IAdvancedVectorStore via SK Qdrant connector for new vector code. Skill registry ops retained as direct Qdrant calls.")]
public sealed partial class QdrantSkillRegistry : ISkillRegistry, IAsyncDisposable
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

    /// <summary>
    /// Initializes a new instance using the DI-provided client and collection registry.
    /// </summary>
    public QdrantSkillRegistry(
        QdrantClient client,
        IQdrantCollectionRegistry registry,
        QdrantSettings settings,
        IEmbeddingModel? embedding = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(settings);
        _embedding = embedding;
        _config = new QdrantSkillConfig(
            ConnectionString: settings.GrpcEndpoint,
            CollectionName: registry.GetCollectionName(QdrantCollectionRole.Skills),
            VectorSize: settings.DefaultVectorSize);
        _disposeClient = false;
    }

    [Obsolete("Use the constructor accepting QdrantClient and IQdrantCollectionRegistry.")]
    public QdrantSkillRegistry(
        QdrantClient client,
        IEmbeddingModel? embedding = null,
        QdrantSkillConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
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

        await EnsureCollectionExistsAsync(ct).ConfigureAwait(false);
        await LoadSkillsFromQdrantAsync(ct).ConfigureAwait(false);
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
            await SaveSkillToQdrantAsync(skill, ct).ConfigureAwait(false);
            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException) // Intentional: Qdrant gRPC + embedding operations
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
        catch (ArgumentException ex)
        {
            return Task.FromResult(Result<AgentSkill, string>.Failure($"Failed to get skill: {ex.Message}"));
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
            await SaveSkillToQdrantAsync(skill, ct).ConfigureAwait(false);

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException) // Intentional: Qdrant gRPC + embedding operations
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
                await SaveSkillToQdrantAsync(updated, ct).ConfigureAwait(false);
            }

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException) // Intentional: Qdrant gRPC save operations
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
                var collectionExists = await _client.CollectionExistsAsync(_config.CollectionName, ct).ConfigureAwait(false);
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
                        cancellationToken: ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is not OperationCanceledException) // Intentional: Qdrant delete best-effort
            {
                Trace.TraceWarning("[WARN] Failed to delete skill '{0}' from Qdrant: {1}", skillId, ex.Message);
            }

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
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
        catch (InvalidOperationException ex)
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

    public ValueTask DisposeAsync()
    {
        _syncLock.Dispose();
        if (_disposeClient)
        {
            _client.Dispose();
        }
        return ValueTask.CompletedTask;
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

}