#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Persistent Memory Store - Persistence and Internal Helpers
// Importance scoring, consolidation, forgetting, vector store, and disk I/O
// ==========================================================

using System.Text.Json;
using LangChain.Databases;
using Ouroboros.Agent.Json;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Persistence and internal helper methods for PersistentMemoryStore.
/// </summary>
public sealed partial class PersistentMemoryStore
{
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

            string json = JsonSerializer.Serialize(data, JsonDefaults.Indented);

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
