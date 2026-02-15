#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Memory Store Implementation
// Persistent long-term learning storage
// ==========================================================

using System.Collections.Concurrent;
using LangChain.Databases;
using Ouroboros.Abstractions;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Implementation of persistent memory store for continual learning.
/// Uses in-memory storage with optional vector similarity search.
/// </summary>
public sealed class MemoryStore : IMemoryStore
{
    private readonly ConcurrentDictionary<string, Experience> _experiences = new();
    private readonly IEmbeddingModel? _embedding;
    private readonly TrackedVectorStore? _vectorStore;

    public MemoryStore(IEmbeddingModel? embedding = null, TrackedVectorStore? vectorStore = null)
    {
        _embedding = embedding;
        _vectorStore = vectorStore;
    }

    /// <summary>
    /// Stores an experience in long-term memory.
    /// </summary>
    public async Task<Result<Unit, string>> StoreExperienceAsync(Experience experience, CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(experience);

            if (string.IsNullOrWhiteSpace(experience.Id))
                return Result<Unit, string>.Failure("Experience ID cannot be empty");

            _experiences[experience.Id] = experience;

            // If vector store available, store for similarity search
            if (_embedding != null && _vectorStore != null)
            {
                string text = $"Context: {experience.Context}\nAction: {experience.Action}\nOutcome: {experience.Outcome}\nSuccess: {experience.Success}";
                float[] embedding = await _embedding.CreateEmbeddingsAsync(text, ct);

                Vector vector = new Vector
                {
                    Id = experience.Id,
                    Text = text,
                    Embedding = embedding,
                    Metadata = new Dictionary<string, object>
                    {
                        ["context"] = experience.Context,
                        ["action"] = experience.Action,
                        ["success"] = experience.Success,
                        ["timestamp"] = experience.Timestamp
                    }
                };

                await _vectorStore.AddAsync(new[] { vector }, ct);
            }

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to store experience: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves relevant experiences based on similarity.
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
                float[] queryEmbedding = await _embedding.CreateEmbeddingsAsync(query.ContextSimilarity, ct);
                IReadOnlyCollection<LangChain.DocumentLoaders.Document> similarDocs = await _vectorStore.GetSimilarDocuments(
                    _embedding,
                    query.ContextSimilarity,
                    amount: query.MaxResults);

                List<Experience> experiences = new List<Experience>();
                foreach (LangChain.DocumentLoaders.Document doc in similarDocs)
                {
                    if (doc.Metadata?.TryGetValue("id", out object? idObj) == true &&
                        idObj?.ToString() is string id &&
                        _experiences.TryGetValue(id, out Experience? exp))
                    {
                        // Apply filters
                        if (query.SuccessOnly.HasValue && exp.Success != query.SuccessOnly.Value)
                            continue;

                        if (query.FromDate.HasValue && exp.Timestamp < query.FromDate.Value)
                            continue;

                        if (query.ToDate.HasValue && exp.Timestamp > query.ToDate.Value)
                            continue;

                        if (query.Tags != null && query.Tags.Count > 0 &&
                            !query.Tags.Any(tag => exp.Tags.Contains(tag)))
                            continue;

                        experiences.Add(exp);
                    }
                }

                return Result<IReadOnlyList<Experience>, string>.Success(experiences);
            }

            // Fallback to filtering
            IEnumerable<Experience> filtered = _experiences.Values;

            if (query.Tags != null && query.Tags.Count > 0)
            {
                filtered = filtered.Where(e => query.Tags.Any(tag => e.Tags.Contains(tag)));
            }

            if (query.SuccessOnly.HasValue)
            {
                filtered = filtered.Where(e => e.Success == query.SuccessOnly.Value);
            }

            if (query.FromDate.HasValue)
            {
                filtered = filtered.Where(e => e.Timestamp >= query.FromDate.Value);
            }

            if (query.ToDate.HasValue)
            {
                filtered = filtered.Where(e => e.Timestamp <= query.ToDate.Value);
            }

            List<Experience> matches = filtered
                .OrderByDescending(exp => exp.Timestamp)
                .Take(query.MaxResults)
                .ToList();

            return Result<IReadOnlyList<Experience>, string>.Success(matches);
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
            List<Experience> experiences = _experiences.Values.ToList();

            int totalCount = experiences.Count;
            int successCount = experiences.Count(e => e.Success);
            int failCount = totalCount - successCount;

            int uniqueContexts = experiences.Select(e => e.Context).Distinct().Count();
            int uniqueTags = experiences.SelectMany(e => e.Tags).Distinct().Count();

            DateTime? oldestExperience = experiences.Any() ? experiences.Min(e => e.Timestamp) : null;
            DateTime? newestExperience = experiences.Any() ? experiences.Max(e => e.Timestamp) : null;

            MemoryStatistics stats = new MemoryStatistics(
                TotalExperiences: totalCount,
                SuccessfulExperiences: successCount,
                FailedExperiences: failCount,
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
                await _vectorStore.ClearAsync(ct);
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

            _experiences.TryGetValue(id, out Experience? experience);
            if (experience != null)
            {
                return Task.FromResult(Result<Experience, string>.Success(experience));
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
    public Task<Result<Unit, string>> DeleteExperienceAsync(string id, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
                return Task.FromResult(Result<Unit, string>.Failure("Experience ID cannot be empty"));

            bool removed = _experiences.TryRemove(id, out _);
            if (!removed)
            {
                return Task.FromResult(Result<Unit, string>.Failure($"Experience with ID '{id}' not found"));
            }

            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<Unit, string>.Failure($"Failed to delete experience: {ex.Message}"));
        }
    }
}
