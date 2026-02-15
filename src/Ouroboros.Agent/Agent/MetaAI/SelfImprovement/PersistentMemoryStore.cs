#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Persistent Memory Store Implementation
// Enhanced memory with persistence, consolidation, and forgetting
// ==========================================================

using System.Collections.Concurrent;
using System.Text.Json;
using LangChain.Databases;
using Ouroboros.Abstractions;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Memory type classification.
/// </summary>
public enum MemoryType
{
    /// <summary>Specific execution instances (recent experiences)</summary>
    Episodic,

    /// <summary>Generalized knowledge and patterns (consolidated)</summary>
    Semantic
}

/// <summary>
/// Configuration for persistent memory behavior.
/// </summary>
public sealed record PersistentMemoryConfig(
    int ShortTermCapacity = 100,
    int LongTermCapacity = 1000,
    double ConsolidationThreshold = 0.7,
    TimeSpan ConsolidationInterval = default,
    bool EnableForgetting = true,
    double ForgettingThreshold = 0.3);

/// <summary>
/// Enhanced memory store with persistence, consolidation, and intelligent forgetting.
/// Implements short-term → long-term memory transfer and episodic/semantic separation.
/// </summary>
public sealed class PersistentMemoryStore : IMemoryStore
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
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to delete experience: {ex.Message}");
        }
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

    /// <summary>
    /// Calculates importance score for an experience.
    /// Based on success, recency, and tag diversity.
    /// </summary>
    private double CalculateImportance(Experience experience)
    {
        // Base importance from success
        double successScore = experience.Success ? 0.7 : 0.3;

        // Recency bonus (newer memories are more important initially)
        double recencyHours = (DateTime.UtcNow - experience.Timestamp).TotalHours;
        double recencyBonus = Math.Max(0, 1.0 - (recencyHours / 24.0)); // Decays over 24 hours

        // Tag diversity bonus (more tags = potentially more useful)
        double tagBonus = Math.Min(0.2, experience.Tags.Count * 0.05);

        // Combined importance (weighted sum)
        double importance = (successScore * 0.5) + (recencyBonus * 0.3) + (tagBonus * 0.2);

        return Math.Clamp(importance, 0.0, 1.0);
    }

    /// <summary>
    /// Determines if consolidation should occur.
    /// </summary>
    private bool ShouldConsolidate()
    {
        // Check time-based consolidation
        if ((DateTime.UtcNow - _lastConsolidation) < _config.ConsolidationInterval)
            return false;

        // Check capacity-based consolidation
        int episodicCount = _experiences.Values.Count(e => e.type == MemoryType.Episodic);
        return episodicCount > _config.ShortTermCapacity;
    }

    /// <summary>
    /// Consolidates short-term episodic memories into long-term semantic memories.
    /// </summary>
    private async Task ConsolidateMemoriesAsync(CancellationToken ct = default)
    {
        _lastConsolidation = DateTime.UtcNow;

        // Find high-importance episodic memories to consolidate
        List<(Experience experience, MemoryType type, double importance)> toConsolidate = _experiences.Values
            .Where(e => e.type == MemoryType.Episodic && e.importance >= _config.ConsolidationThreshold)
            .OrderByDescending(e => e.importance)
            .Take(_config.ShortTermCapacity / 2)
            .ToList();

        foreach ((Experience experience, MemoryType _, double importance) in toConsolidate)
        {
            // Mark as semantic (long-term)
            _experiences[experience.Id] = (experience, MemoryType.Semantic, importance);

            // Update in vector store if available
            if (_embedding != null && _vectorStore != null)
            {
                await StoreInVectorStoreAsync(experience, MemoryType.Semantic, ct);
            }
        }
    }

    /// <summary>
    /// Removes low-importance memories to prevent unbounded growth.
    /// </summary>
    private async Task ForgetLowImportanceMemoriesAsync()
    {
        List<(Experience experience, MemoryType type, double importance)> toForget = _experiences.Values
            .Where(e => e.importance < _config.ForgettingThreshold)
            .OrderBy(e => e.importance)
            .Take(_experiences.Count - _config.LongTermCapacity)
            .ToList();

        foreach ((Experience experience, MemoryType _, double _) in toForget)
        {
            _experiences.TryRemove(experience.Id, out _);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stores experience in vector database.
    /// </summary>
    private async Task StoreInVectorStoreAsync(
        Experience experience,
        MemoryType type,
        CancellationToken ct)
    {
        if (_embedding == null || _vectorStore == null)
            return;

        string text = $"[{type}] Context: {experience.Context}\n" +
                   $"Action: {experience.Action}\n" +
                   $"Outcome: {experience.Outcome}\n" +
                   $"Success: {experience.Success}\n" +
                   $"Tags: {string.Join(", ", experience.Tags)}";

        float[] embedding = await _embedding.CreateEmbeddingsAsync(text, ct);

        Vector vector = new Vector
        {
            Id = experience.Id,
            Text = text,
            Embedding = embedding,
            Metadata = new Dictionary<string, object>
            {
                ["id"] = experience.Id,
                ["context"] = experience.Context,
                ["action"] = experience.Action,
                ["success"] = experience.Success,
                ["timestamp"] = experience.Timestamp,
                ["memory_type"] = type.ToString()
            }
        };

        await _vectorStore.AddAsync(new[] { vector }, ct);
    }

    /// <summary>
    /// Retrieves experiences using vector similarity search.
    /// </summary>
    private async Task<Result<IReadOnlyList<Experience>, string>> RetrieveViaSimilarityAsync(
        MemoryQuery query,
        CancellationToken ct)
    {
        if (_embedding == null || _vectorStore == null || string.IsNullOrEmpty(query.ContextSimilarity))
            return Result<IReadOnlyList<Experience>, string>.Success(Array.Empty<Experience>());

        try
        {
            float[] queryEmbedding = await _embedding.CreateEmbeddingsAsync(query.ContextSimilarity, ct);

            IReadOnlyCollection<LangChain.DocumentLoaders.Document> searchResults = await _vectorStore.GetSimilarDocuments(
                _embedding,
                query.ContextSimilarity,
                amount: query.MaxResults);

            List<Experience> experiences = new List<Experience>();
            foreach (LangChain.DocumentLoaders.Document doc in searchResults)
            {
                if (doc.Metadata?.TryGetValue("id", out object? idObj) == true &&
                    idObj?.ToString() is string id &&
                    _experiences.TryGetValue(id, out (Experience experience, MemoryType type, double importance) entry))
                {
                    // Apply additional filters
                    if (query.SuccessOnly.HasValue && entry.experience.Success != query.SuccessOnly.Value)
                        continue;

                    if (query.FromDate.HasValue && entry.experience.Timestamp < query.FromDate.Value)
                        continue;

                    if (query.ToDate.HasValue && entry.experience.Timestamp > query.ToDate.Value)
                        continue;

                    if (query.Tags != null && query.Tags.Count > 0 &&
                        !query.Tags.Any(tag => entry.experience.Tags.Contains(tag)))
                        continue;

                    experiences.Add(entry.experience);
                }
            }

            return Result<IReadOnlyList<Experience>, string>.Success(experiences);
        }
        catch
        {
            // Fallback to simple retrieval
            List<Experience> fallbackExperiences = _experiences.Values
                .Select(e => e.experience)
                .Take(query.MaxResults)
                .ToList();

            return Result<IReadOnlyList<Experience>, string>.Success(fallbackExperiences);
        }
    }

    /// <summary>
    /// Saves experiences to disk as JSON.
    /// </summary>
    private async Task SaveToDiskAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_persistencePath))
            return;

        try
        {
            string? directory = Path.GetDirectoryName(_persistencePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var data = _experiences.Values.Select(v => new
            {
                Experience = v.experience,
                Type = v.type.ToString(),
                Importance = v.importance
            }).ToList();

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_persistencePath, json, ct);
        }
        catch
        {
            // Silent failure - persistence is optional
        }
    }

    /// <summary>
    /// Loads experiences from disk.
    /// </summary>
    private async Task LoadFromDiskAsync()
    {
        if (string.IsNullOrEmpty(_persistencePath) || !File.Exists(_persistencePath))
            return;

        try
        {
            string json = await File.ReadAllTextAsync(_persistencePath);
            var data = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);

            if (data != null)
            {
                foreach (var item in data)
                {
                    if (item.TryGetValue("Experience", out JsonElement expElement))
                    {
                        var experience = JsonSerializer.Deserialize<Experience>(expElement.GetRawText());
                        if (experience != null)
                        {
                            MemoryType type = item.TryGetValue("Type", out JsonElement typeElement) &&
                                            Enum.TryParse<MemoryType>(typeElement.GetString(), out MemoryType parsedType)
                                ? parsedType
                                : MemoryType.Episodic;

                            double importance = item.TryGetValue("Importance", out JsonElement impElement) &&
                                              impElement.TryGetDouble(out double imp)
                                ? imp
                                : 0.5;

                            _experiences[experience.Id] = (experience, type, importance);
                        }
                    }
                }
            }
        }
        catch
        {
            // Silent failure - if we can't load, start fresh
        }
    }
}
