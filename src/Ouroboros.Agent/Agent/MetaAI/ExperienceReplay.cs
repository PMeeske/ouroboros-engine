// ==========================================================
// Experience Replay - Train on stored experiences
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Implementation of experience replay for continual learning.
/// </summary>
public sealed class ExperienceReplay : IExperienceReplay
{
    private readonly IMemoryStore _memory;
    private readonly ISkillRegistry _skills;
    private readonly Ouroboros.Abstractions.Core.IChatCompletionModel _llm;

    public ExperienceReplay(
        IMemoryStore memory,
        ISkillRegistry skills,
        Ouroboros.Abstractions.Core.IChatCompletionModel llm)
    {
        ArgumentNullException.ThrowIfNull(memory);
        _memory = memory;
        ArgumentNullException.ThrowIfNull(skills);
        _skills = skills;
        ArgumentNullException.ThrowIfNull(llm);
        _llm = llm;
    }

    /// <summary>
    /// Trains the orchestrator on stored experiences.
    /// </summary>
    public async Task<Result<TrainingResult, string>> TrainOnExperiencesAsync(
        ExperienceReplayConfig? config = null,
        CancellationToken ct = default)
    {
        config ??= new ExperienceReplayConfig();

        try
        {
            // Select experiences for training
            List<Experience> experiences = await SelectTrainingExperiencesAsync(config, ct).ConfigureAwait(false);

            if (experiences.Count == 0)
            {
                return Result<TrainingResult, string>.Success(
                    new TrainingResult(0, new Dictionary<string, double>(), new List<string>(), true));
            }

            // Analyze patterns
            List<string> patterns = await AnalyzeExperiencePatternsAsync(experiences, ct).ConfigureAwait(false);

            // Extract skills from high-quality experiences
            int skillsExtracted = 0;
            foreach (Experience? exp in experiences.Where(e => e.Verification.QualityScore > 0.8))
            {
                string skillName = $"learned_skill_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                Result<Skill, string> skillResult = await _skills.ExtractSkillAsync(
                    exp.Execution,
                    skillName,
                    $"Learned from goal: {exp.Goal}").ConfigureAwait(false);

                if (skillResult.IsSuccess)
                {
                    skillsExtracted++;
                }
            }

            // Calculate improved metrics
            Dictionary<string, double> improvedMetrics = new Dictionary<string, double>
            {
                ["patterns_discovered"] = patterns.Count,
                ["skills_extracted"] = skillsExtracted,
                ["avg_quality"] = experiences.Average(e => e.Verification.QualityScore)
            };

            TrainingResult result = new TrainingResult(
                experiences.Count,
                improvedMetrics,
                patterns,
                Success: true);

            return Result<TrainingResult, string>.Success(result);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<TrainingResult, string>.Failure($"Training failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Analyzes experiences to extract patterns.
    /// </summary>
    public async Task<List<string>> AnalyzeExperiencePatternsAsync(
        List<Experience> experiences,
        CancellationToken ct = default)
    {
        List<string> patterns = new List<string>();

        try
        {
            // Group experiences by goal similarity
            IEnumerable<IGrouping<string, Experience>> goalGroups = experiences
                .GroupBy(e => ExtractGoalType(e.Goal))
                .Where(g => g.Count() > 1);

            foreach (IGrouping<string, Experience>? group in goalGroups)
            {
                // Find common successful patterns
                List<Experience> successfulExperiences = group.Where(e => e.Verification.Verified).ToList();

                if (successfulExperiences.Count > 0)
                {
                    List<string> commonActions = FindCommonActions(successfulExperiences);
                    if (commonActions.Any())
                    {
                        patterns.Add($"Pattern for {group.Key}: {string.Join(" -> ", commonActions)}");
                    }
                }
            }

            // Use LLM to identify deeper patterns if available
            if (patterns.Any())
            {
                string patternPrompt = BuildPatternAnalysisPrompt(experiences);
                string analysis = await _llm.GenerateTextAsync(patternPrompt, ct).ConfigureAwait(false);

                // Extract insights from LLM analysis
                List<string> insights = ExtractInsights(analysis);
                patterns.AddRange(insights);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            // Fallback to simple pattern detection
        }

        return patterns.Distinct().ToList();
    }

    /// <summary>
    /// Selects experiences for training based on priority.
    /// </summary>
    public async Task<List<Experience>> SelectTrainingExperiencesAsync(
        ExperienceReplayConfig config,
        CancellationToken ct = default)
    {
                _ = await _memory.GetStatisticsAsync().ConfigureAwait(false);

        // Get all experiences and filter
        MemoryQuery query = new MemoryQuery(
            Tags: null,
            ContextSimilarity: null,
            MaxResults: config.MaxExperiences,
            MinSimilarity: 0.0);

        var experiencesResult = await _memory.RetrieveRelevantExperiencesAsync(query, ct).ConfigureAwait(false);
        List<Experience> allExperiences = experiencesResult.IsSuccess ? experiencesResult.Value.ToList() : new List<Experience>();

        // Filter by quality
        List<Experience> qualityFiltered = allExperiences
            .Where(e => e.Verification.QualityScore >= config.MinQualityScore)
            .ToList();

        // Prioritize based on configuration
        if (config.PrioritizeHighQuality)
        {
            qualityFiltered = qualityFiltered
                .OrderByDescending(e => e.Verification.QualityScore)
                .ThenByDescending(e => e.Timestamp)
                .Take(config.BatchSize)
                .ToList();
        }
        else
        {
            // Diverse sampling - mix of quality levels
            qualityFiltered = qualityFiltered
                .OrderBy(_ => Guid.NewGuid()) // Random sampling
                .Take(config.BatchSize)
                .ToList();
        }

        return qualityFiltered;
    }

    private static string ExtractGoalType(string goal)
    {
        // Simple categorization - in production use more sophisticated NLP
        string goalLower = goal.ToLowerInvariant();

        if (goalLower.Contains("calculate") || goalLower.Contains("compute"))
            return "calculation";
        if (goalLower.Contains("analyze") || goalLower.Contains("examine"))
            return "analysis";
        if (goalLower.Contains("create") || goalLower.Contains("generate"))
            return "creation";
        if (goalLower.Contains("explain") || goalLower.Contains("describe"))
            return "explanation";

        return "general";
    }

    private static List<string> FindCommonActions(List<Experience> experiences)
    {
        // Find actions that appear in all successful experiences
        List<List<string>> actionLists = experiences
            .Select(e => e.Plan.Steps.Select(s => s.Action).ToList())
            .ToList();

        if (!actionLists.Any())
            return new List<string>();

        List<string> commonActions = actionLists
            .SelectMany(actions => actions)
            .GroupBy(a => a)
            .Where(g => g.Count() >= actionLists.Count * 0.5) // Present in at least 50%
            .Select(g => g.Key)
            .ToList();

        return commonActions;
    }

    private static string BuildPatternAnalysisPrompt(List<Experience> experiences)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("Analyze the following successful experiences and identify common patterns:\n\n");

        foreach (Experience? exp in experiences.Take(5))
        {
            sb.Append($"Goal: {exp.Goal}\n");
            sb.Append($"Steps: {string.Join(" -> ", exp.Plan.Steps.Select(s => s.Action))}\n");
            sb.Append($"Quality: {exp.Verification.QualityScore:P0}\n\n");
        }

        sb.Append("What are the common successful patterns? List them briefly.");

        return sb.ToString();
    }

    private static List<string> ExtractInsights(string analysis)
    {
        // Simple extraction - in production use more sophisticated parsing
        List<string> insights = new List<string>();
        string[] lines = analysis.Split('\n');

        foreach (string line in lines.Where(l => l.Trim().StartsWith('-') || l.Trim().StartsWith('\u2022')))
        {
            insights.Add(line.Trim().TrimStart('-', '\u2022').Trim());
        }

        return insights;
    }
}
