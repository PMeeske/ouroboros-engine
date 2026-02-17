namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Extension methods to integrate external knowledge into the emergence pipeline.
/// </summary>
public static class ExternalKnowledgeExtensions
{
    /// <summary>
    /// Enriches the hypothesis engine with observations from external research.
    /// </summary>
    public static async Task<Result<Hypothesis, string>> GenerateHypothesisFromResearchAsync(
        this IHypothesisEngine hypothesisEngine,
        IExternalKnowledgeSource knowledgeSource,
        string researchTopic,
        CancellationToken ct = default)
    {
        // Fetch papers on the topic
        var papersResult = await knowledgeSource.SearchPapersAsync(researchTopic, maxResults: 5, ct);
        if (!papersResult.IsSuccess)
        {
            return Result<Hypothesis, string>.Failure($"Failed to fetch research: {papersResult.Error}");
        }

        // Extract observations from papers
        List<string> observations = await knowledgeSource.ExtractObservationsAsync(papersResult.Value, ct);

        if (observations.Count == 0)
        {
            return Result<Hypothesis, string>.Failure("No observations could be extracted from research");
        }

        // Use abductive reasoning to generate hypothesis
        return await hypothesisEngine.AbductiveReasoningAsync(observations, ct);
    }

    /// <summary>
    /// Adds research-based exploration opportunities to the curiosity engine.
    /// </summary>
    public static async Task<List<ExplorationOpportunity>> EnrichWithResearchOpportunitiesAsync(
        this ICuriosityEngine curiosityEngine,
        IExternalKnowledgeSource knowledgeSource,
        string domain,
        CancellationToken ct = default)
    {
        // Get research-based opportunities
        var researchOpportunities = await knowledgeSource.IdentifyResearchOpportunitiesAsync(domain, 5, ct);

        // Get existing curiosity-based opportunities
        var curiosityOpportunities = await curiosityEngine.IdentifyExplorationOpportunitiesAsync(5, ct);

        // Merge and deduplicate
        var allOpportunities = researchOpportunities
            .Concat(curiosityOpportunities)
            .OrderByDescending(o => o.NoveltyScore * 0.4 + o.InformationGainEstimate * 0.6)
            .Take(10)
            .ToList();

        return allOpportunities;
    }
}