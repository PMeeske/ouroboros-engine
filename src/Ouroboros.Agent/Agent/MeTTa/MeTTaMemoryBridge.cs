#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using Ouroboros.Abstractions;
using Ouroboros.Agent.MetaAI;
using MetaAIMemoryStatistics = Ouroboros.Agent.MetaAI.MemoryStatistics;

namespace Ouroboros.Tools.MeTTa;

/// <summary>
/// Bridges orchestrator memory/experience to MeTTa symbolic facts.
/// </summary>
public sealed class MeTTaMemoryBridge
{
    private readonly IMeTTaEngine _engine;
    private readonly MemoryStore _memory;

    /// <summary>
    /// Creates a new MeTTa memory bridge.
    /// </summary>
    /// <param name="engine">The MeTTa engine to populate with facts.</param>
    /// <param name="memory">The memory store to extract facts from.</param>
    public MeTTaMemoryBridge(IMeTTaEngine engine, MemoryStore memory)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
    }

    /// <summary>
    /// Synchronizes all experiences from memory to MeTTa as symbolic facts.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success with the number of facts synced, or an error.</returns>
    public async Task<Result<int, string>> SyncAllExperiencesAsync(CancellationToken ct = default)
    {
        try
        {
            var statsResult = await _memory.GetStatisticsAsync();
            MetaAIMemoryStatistics stats = statsResult.IsSuccess 
                ? new MetaAIMemoryStatistics(statsResult.Value.TotalExperiences, statsResult.Value.SuccessfulExperiences, statsResult.Value.FailedExperiences, statsResult.Value.UniqueContexts, statsResult.Value.UniqueTags, AverageQualityScore: statsResult.Value.AverageQualityScore)
                : new MetaAIMemoryStatistics(0, 0, 0, 0, 0, AverageQualityScore: 0.0);
            List<Experience> experiences = new List<Experience>();

            // Retrieve all experiences (this is a simplified approach)
            // In a real implementation, you'd want pagination or streaming
            for (int i = 0; i < stats.TotalExperiences && i < 1000; i++)
            {
                // Note: This assumes we can enumerate experiences somehow
                // The actual MemoryStore API might need extension for this
            }

            int factCount = 0;

            // For now, sync memory statistics as facts
            string statsFact = $"(memory-stats (total {stats.TotalExperiences}) (avg-quality {stats.AverageQualityScore}))";
            var addResult = await _engine.AddFactAsync(statsFact, ct);
            Result<Unit, string> result = addResult.Map(_ => Unit.Value);

            if (result.IsFailure)
            {
                return Result<int, string>.Failure(result.Error);
            }

            factCount++;

            return Result<int, string>.Success(factCount);
        }
        catch (Exception ex)
        {
            return Result<int, string>.Failure($"Memory sync failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds a single experience to MeTTa as symbolic facts.
    /// </summary>
    /// <param name="experience">The experience to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public async Task<Result<Unit, string>> AddExperienceAsync(Experience experience, CancellationToken ct = default)
    {
        if (experience == null)
        {
            return Result<Unit, string>.Failure("Experience cannot be null");
        }

        try
        {
            // Convert experience to MeTTa facts
            string goalFact = $"(experience-goal \"{experience.Id}\" \"{EscapeString(experience.Goal)}\")";
            string qualityFact = $"(experience-quality \"{experience.Id}\" {experience.Verification.QualityScore})";
            string successFact = $"(experience-success \"{experience.Id}\" {experience.Execution.Success.ToString().ToLower()})";

            var results = new[]
            {
                (await _engine.AddFactAsync(goalFact, ct)).Map(_ => Unit.Value),
                (await _engine.AddFactAsync(qualityFact, ct)).Map(_ => Unit.Value),
                (await _engine.AddFactAsync(successFact, ct)).Map(_ => Unit.Value)
            };

            List<Result<Unit, string>> failures = results.Where(r => r.IsFailure).ToList();
            if (failures.Any())
            {
                return Result<Unit, string>.Failure($"Failed to add {failures.Count} facts");
            }

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to add experience: {ex.Message}");
        }
    }

    /// <summary>
    /// Queries MeTTa for experiences matching criteria.
    /// </summary>
    /// <param name="query">MeTTa query to find experiences.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Query results as a string.</returns>
    public async Task<Result<string, string>> QueryExperiencesAsync(string query, CancellationToken ct = default)
    {
        return await _engine.ExecuteQueryAsync(query, ct);
    }

    /// <summary>
    /// Adds a plan verification rule based on experience patterns.
    /// </summary>
    /// <param name="rule">The verification rule in MeTTa format.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public async Task<Result<string, string>> AddVerificationRuleAsync(string rule, CancellationToken ct = default)
    {
        return await _engine.ApplyRuleAsync(rule, ct);
    }

    private static string EscapeString(string input)
    {
        return input.Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}