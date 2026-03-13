// ==========================================================
// Persistent Skill Registry Implementation
// Stores skills to disk/vector store for persistence across restarts
// ==========================================================

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using LangChain.Databases;
using Ouroboros.Abstractions;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Persistent implementation of skill registry that saves skills to disk and optionally to a vector store.
/// Enables skill persistence across application restarts and semantic skill search via embeddings.
/// </summary>
public sealed partial class PersistentSkillRegistry : ISkillRegistry, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, AgentSkill> _skills = new();
    private readonly IEmbeddingModel? _embedding;
    private readonly TrackedVectorStore? _vectorStore;
    private readonly PersistentSkillConfig _config;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private volatile bool _isDirty;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _isInitialized;

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

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_isInitialized) return;

            await LoadSkillsAsync(ct).ConfigureAwait(false);
            _isInitialized = true;
        }
        finally { _initLock.Release(); }
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

            await SaveSkillsAsync(ct).ConfigureAwait(false);

            if (_embedding != null && _vectorStore != null)
            {
                await AddToVectorStoreAsync(skill, ct).ConfigureAwait(false);
            }

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (OperationCanceledException) { throw; }
        catch (IOException ex)
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
            
            if (!_skills.ContainsKey(skill.Id))
                return Result<Unit, string>.Failure($"Skill '{skill.Id}' not found");

            _skills[skill.Id] = skill;
            _isDirty = true;
            await SaveSkillsAsync(ct).ConfigureAwait(false);

            // Update in vector store if available
            if (_embedding != null && _vectorStore != null)
            {
                await AddToVectorStoreAsync(skill, ct).ConfigureAwait(false);
            }

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (OperationCanceledException) { throw; }
        catch (IOException ex)
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
                await SaveSkillsAsync(ct).ConfigureAwait(false);
            }

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (OperationCanceledException) { throw; }
        catch (IOException ex)
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
                await SaveSkillsAsync(ct).ConfigureAwait(false);
                return Result<Unit, string>.Success(Unit.Value);
            }

            return Result<Unit, string>.Failure($"Skill '{skillId}' not found");
        }
        catch (OperationCanceledException) { throw; }
        catch (IOException ex)
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
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(Result<IReadOnlyList<AgentSkill>, string>.Failure($"Failed to get all skills: {ex.Message}"));
        }
    }

}