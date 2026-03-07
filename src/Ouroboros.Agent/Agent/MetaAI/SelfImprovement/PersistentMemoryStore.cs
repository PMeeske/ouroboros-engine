// ==========================================================
// Persistent Memory Store Implementation
// Enhanced memory with persistence, consolidation, and forgetting
// ==========================================================

using System.Collections.Concurrent;
using System.Text.Json;
using LangChain.Databases;
using Ouroboros.Abstractions;
using Ouroboros.Agent.Json;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Enhanced memory store with persistence, consolidation, and intelligent forgetting.
/// Implements short-term → long-term memory transfer and episodic/semantic separation.
/// </summary>
public sealed partial class PersistentMemoryStore : IMemoryStore
{
    private readonly ConcurrentDictionary<string, (Experience experience, MemoryType type, double importance)> _experiences = new();
    private readonly IEmbeddingModel? _embedding;
    private readonly TrackedVectorStore? _vectorStore;
    private readonly PersistentMemoryConfig _config;
    private DateTime _lastConsolidation = DateTime.UtcNow;
    private readonly string? _persistencePath;

    public PersistentMemoryStore(
        IEmbeddingModel? embedding = null,
        TrackedVectorStore? vectorStore = null,
        PersistentMemoryConfig? config = null,
        string? persistencePath = null)
    {
        _embedding = embedding;
        _vectorStore = vectorStore;
        _config = config ?? new PersistentMemoryConfig(
            ConsolidationInterval: TimeSpan.FromHours(1));
        _persistencePath = persistencePath;

        if (!string.IsNullOrEmpty(_persistencePath))
        {
            // Intentional: sync-over-async in constructor; persistence load must complete before instance is usable
            LoadFromDiskAsync().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Stores an experience in memory with automatic importance scoring.
    /// </summary>
    public async Task<Result<Unit, string>> StoreExperienceAsync(Experience experience, CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(experience);

            if (string.IsNullOrWhiteSpace(experience.Id))
                return Result<Unit, string>.Failure("Experience ID cannot be empty");

            // Calculate importance score
            double importance = CalculateImportance(experience);

            // Store in short-term episodic memory initially
            _experiences[experience.Id] = (experience, MemoryType.Episodic, importance);

            // Store in vector database if available
            if (_embedding != null && _vectorStore != null)
            {
                await StoreInVectorStoreAsync(experience, MemoryType.Episodic, ct);
            }

            // Persist to disk if configured
            if (!string.IsNullOrEmpty(_persistencePath))
            {
                await SaveToDiskAsync(ct);
            }

            // Check if consolidation is needed
            if (ShouldConsolidate())
            {
                await ConsolidateMemoriesAsync(ct);
            }

            // Check if forgetting is needed
            if (_config.EnableForgetting && _experiences.Count > _config.LongTermCapacity)
            {
                await ForgetLowImportanceMemoriesAsync();
            }

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to store experience: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves relevant experiences based on similarity and recency.
    /// </summary>
    public async Task<Result<IReadOnlyList<Experience>, string>> QueryExperiencesAsync(
        MemoryQuery query,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(query);

            // If vector store available and context similarity specified, use semantic search
            if (_embedding != null && _vectorStore != null && !string.IsNullOrEmpty(query.ContextSimilarity))
            {
                return await RetrieveViaSimilarityAsync(query, ct);
            }

            // Fallback to filtering based on query parameters
            IEnumerable<(Experience experience, MemoryType type, double importance)> filtered = _experiences.Values;

            // Filter by tags if specified
            if (query.Tags != null && query.Tags.Count > 0)
            {
                filtered = filtered.Where(e => query.Tags.Any(tag => e.experience.Tags.Contains(tag)));
            }

            // Filter by success if specified
            if (query.SuccessOnly.HasValue)
            {
                filtered = filtered.Where(e => e.experience.Success == query.SuccessOnly.Value);
            }

            // Filter by date range
            if (query.FromDate.HasValue)
            {
                filtered = filtered.Where(e => e.experience.Timestamp >= query.FromDate.Value);
            }

            if (query.ToDate.HasValue)
            {
                filtered = filtered.Where(e => e.experience.Timestamp <= query.ToDate.Value);
            }

            // Order by importance and take max results
            List<Experience> experiences = filtered
                .OrderByDescending(e => e.importance)
                .Take(query.MaxResults)
                .Select(e => e.experience)
                .ToList();

            return Result<IReadOnlyList<Experience>, string>.Success(experiences);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Experience>, string>.Failure($"Failed to query experiences: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets memory statistics.
    /// </summary>
    public Task<Result<MemoryStatistics, string>> GetStatisticsAsync(CancellationToken ct = default)
    {
        try
        {
            List<Experience> experiences = _experiences.Values.Select(v => v.experience).ToList();

            int totalExperiences = experiences.Count;
            int successfulExperiences = experiences.Count(e => e.Success);
            int failedExperiences = totalExperiences - successfulExperiences;

            int uniqueContexts = experiences.Select(e => e.Context).Distinct().Count();
            int uniqueTags = experiences.SelectMany(e => e.Tags).Distinct().Count();

            DateTime? oldestExperience = experiences.Any() ? experiences.Min(e => e.Timestamp) : null;
            DateTime? newestExperience = experiences.Any() ? experiences.Max(e => e.Timestamp) : null;

            MemoryStatistics stats = new MemoryStatistics(
                TotalExperiences: totalExperiences,
                SuccessfulExperiences: successfulExperiences,
                FailedExperiences: failedExperiences,
                UniqueContexts: uniqueContexts,
                UniqueTags: uniqueTags,
                OldestExperience: oldestExperience,
                NewestExperience: newestExperience);

            return Task.FromResult(Result<MemoryStatistics, string>.Success(stats));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Task.FromResult(Result<MemoryStatistics, string>.Failure($"Failed to get statistics: {ex.Message}"));
        }
    }

    /// <summary>
    /// Clears all experiences from memory.
    /// </summary>
    public async Task<Result<Unit, string>> ClearAsync(CancellationToken ct = default)
    {
        try
        {
            _experiences.Clear();

            if (_vectorStore != null)
            {
                // Note: TrackedVectorStore doesn't have a Clear method
                // In a real implementation with Qdrant, we would clear the collection
                await Task.CompletedTask;
            }

            // Clear persisted data if configured
            if (!string.IsNullOrEmpty(_persistencePath) && File.Exists(_persistencePath))
            {
                File.Delete(_persistencePath);
            }

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to clear memory: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets an experience by ID.
    /// </summary>
    public Task<Result<Experience, string>> GetExperienceAsync(string id, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
                return Task.FromResult(Result<Experience, string>.Failure("Experience ID cannot be empty"));

            bool found = _experiences.TryGetValue(id, out (Experience experience, MemoryType type, double importance) entry);
            if (found)
            {
                return Task.FromResult(Result<Experience, string>.Success(entry.experience));
            }
            else
            {
                return Task.FromResult(Result<Experience, string>.Failure($"Experience with ID '{id}' not found"));
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Task.FromResult(Result<Experience, string>.Failure($"Failed to get experience: {ex.Message}"));
        }
    }

    /// <summary>
    /// Deletes an experience from memory.
    /// </summary>
    public async Task<Result<Unit, string>> DeleteExperienceAsync(string id, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
                return Result<Unit, string>.Failure("Experience ID cannot be empty");

            bool removed = _experiences.TryRemove(id, out _);
            if (!removed)
            {
                return Result<Unit, string>.Failure($"Experience with ID '{id}' not found");
            }

            // Persist changes if configured
            if (!string.IsNullOrEmpty(_persistencePath))
            {
                await SaveToDiskAsync(ct);
            }

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to delete experience: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves relevant experiences based on a query (alias for QueryExperiencesAsync).
    /// </summary>
    public Task<Result<IReadOnlyList<Experience>, string>> RetrieveRelevantExperiencesAsync(
        MemoryQuery query,
        CancellationToken ct = default)
    {
        return QueryExperiencesAsync(query, ct);
    }
    /// <summary>
    /// Gets statistics (returns MemoryStatistics directly).
    /// </summary>
    public async Task<MemoryStatistics> GetStatsAsync(CancellationToken ct = default)
    {
        var result = await GetStatisticsAsync(ct);
        return result.IsSuccess
            ? result.Value
            : new MemoryStatistics(0, 0, 0, 0, 0);
    }
    /// <summary>
    /// Gets experiences by memory type.
    /// </summary>
    public List<Experience> GetExperiencesByType(MemoryType type)
    {
        return _experiences.Values
            .Where(e => e.type == type)
            .Select(e => e.experience)
            .ToList();
    }

}

