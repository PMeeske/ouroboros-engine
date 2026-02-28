#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Ouroboros.Agent.MetaAI.MetaLearning;

public sealed partial class MetaLearner
{
    /// <summary>
    /// Evaluates how well current learning approach works.
    /// </summary>
    public async Task<Result<LearningEfficiencyReport, string>> EvaluateLearningEfficiencyAsync(
        TimeSpan evaluationWindow,
        CancellationToken ct = default)
    {
        try
        {
            DateTime cutoffTime = DateTime.UtcNow - evaluationWindow;
            List<LearningEpisode> recentEpisodes = _episodes
                .Where(e => e.CompletedAt >= cutoffTime)
                .ToList();

            if (!recentEpisodes.Any())
            {
                return Result<LearningEfficiencyReport, string>.Failure(
                    "No learning episodes in the evaluation window");
            }

            // Calculate metrics
            double avgIterations = recentEpisodes.Average(e => e.IterationsRequired);
            double avgExamples = recentEpisodes.Average(e => e.ExamplesProvided);
            double successRate = recentEpisodes.Count(e => e.Successful) / (double)recentEpisodes.Count;

            // Calculate learning speed trend
            List<LearningEpisode> ordered = recentEpisodes.OrderBy(e => e.StartedAt).ToList();
            double learningSpeedTrend = CalculateLearningSpeedTrend(ordered);

            // Group by task type
            Dictionary<string, double> efficiencyByTaskType = recentEpisodes
                .GroupBy(e => e.TaskType)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count(e => e.Successful) / (double)g.Count());

            // Identify bottlenecks
            List<string> bottlenecks = await IdentifyBottlenecksAsync(recentEpisodes, ct);

            // Generate recommendations
            List<string> recommendations = await GenerateRecommendationsAsync(
                recentEpisodes,
                successRate,
                learningSpeedTrend,
                ct);

            LearningEfficiencyReport report = new LearningEfficiencyReport(
                AverageIterationsToLearn: avgIterations,
                AverageExamplesNeeded: avgExamples,
                SuccessRate: successRate,
                LearningSpeedTrend: learningSpeedTrend,
                EfficiencyByTaskType: efficiencyByTaskType,
                Bottlenecks: bottlenecks,
                Recommendations: recommendations);

            return Result<LearningEfficiencyReport, string>.Success(report);
        }
        catch (Exception ex)
        {
            return Result<LearningEfficiencyReport, string>.Failure(
                $"Learning efficiency evaluation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Identifies transferable meta-knowledge from past learning.
    /// </summary>
    public async Task<Result<List<MetaKnowledge>, string>> ExtractMetaKnowledgeAsync(
        CancellationToken ct = default)
    {
        try
        {
            List<MetaKnowledge> metaKnowledge = new List<MetaKnowledge>();
            List<LearningEpisode> allEpisodes = _episodes.ToList();

            if (!allEpisodes.Any())
            {
                return Result<List<MetaKnowledge>, string>.Success(metaKnowledge);
            }

            // Extract knowledge by task type
            Dictionary<string, List<LearningEpisode>> byTaskType = allEpisodes
                .GroupBy(e => e.TaskType)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (KeyValuePair<string, List<LearningEpisode>> taskGroup in byTaskType)
            {
                List<LearningEpisode> successful = taskGroup.Value.Where(e => e.Successful).ToList();

                if (successful.Count < 3)
                    continue; // Need minimum examples

                // Extract patterns using LLM
                string prompt = $@"Analyze these successful learning episodes and extract transferable insights:

TASK TYPE: {taskGroup.Key}
EPISODES: {successful.Count}
SUCCESS RATE: {(successful.Count / (double)taskGroup.Value.Count):P0}

SAMPLE EPISODES:
{string.Join("\n", successful.Take(5).Select(e => $"- {e.TaskDescription}: {e.IterationsRequired} iterations, {e.FinalPerformance:P0} performance"))}

Extract 1-2 key insights that could apply to similar tasks.
Format each as:
INSIGHT: [concise insight]
CONFIDENCE: [0-1]
APPLICABLE_TO: [task types, comma-separated]";

                try
                {
                    string response = await _llm.GenerateTextAsync(prompt, ct);
                    List<MetaKnowledge> insights = ParseMetaKnowledgeResponse(
                        response,
                        taskGroup.Key,
                        successful.Count);

                    metaKnowledge.AddRange(insights);
                }
                catch
                {
                    // Continue with other task types
                }
            }

            // Add general insights
            if (allEpisodes.Count >= 20)
            {
                double overallSuccessRate = allEpisodes.Count(e => e.Successful) / (double)allEpisodes.Count;

                if (overallSuccessRate >= 0.7)
                {
                    metaKnowledge.Add(new MetaKnowledge(
                        Domain: "General",
                        Insight: "Current learning strategies are effective across task types",
                        Confidence: overallSuccessRate,
                        SupportingExamples: allEpisodes.Count,
                        ApplicableTaskTypes: byTaskType.Keys.ToList(),
                        DiscoveredAt: DateTime.UtcNow));
                }
            }

            return Result<List<MetaKnowledge>, string>.Success(metaKnowledge);
        }
        catch (Exception ex)
        {
            return Result<List<MetaKnowledge>, string>.Failure(
                $"Meta-knowledge extraction failed: {ex.Message}");
        }
    }

    private double CalculateLearningSpeedTrend(List<LearningEpisode> orderedEpisodes)
    {
        if (orderedEpisodes.Count < 2)
            return 0.0;

        int midpoint = orderedEpisodes.Count / 2;
        List<LearningEpisode> firstHalf = orderedEpisodes.Take(midpoint).ToList();
        List<LearningEpisode> secondHalf = orderedEpisodes.Skip(midpoint).ToList();

        double firstHalfAvgIterations = firstHalf.Average(e => e.IterationsRequired);
        double secondHalfAvgIterations = secondHalf.Average(e => e.IterationsRequired);

        double trend = (firstHalfAvgIterations - secondHalfAvgIterations) / firstHalfAvgIterations;
        return trend;
    }

    private Task<List<string>> IdentifyBottlenecksAsync(
        List<LearningEpisode> episodes,
        CancellationToken ct)
    {
        List<string> bottlenecks = new List<string>();

        double avgIterations = episodes.Average(e => e.IterationsRequired);
        if (avgIterations > 100)
        {
            bottlenecks.Add($"High iteration count: averaging {avgIterations:F0} iterations");
        }

        double successRate = episodes.Count(e => e.Successful) / (double)episodes.Count;
        if (successRate < 0.6)
        {
            bottlenecks.Add($"Low success rate: {successRate:P0}");
        }

        Dictionary<string, double> taskTypeSuccess = episodes
            .GroupBy(e => e.TaskType)
            .ToDictionary(
                g => g.Key,
                g => g.Count(e => e.Successful) / (double)g.Count());

        foreach (KeyValuePair<string, double> kvp in taskTypeSuccess)
        {
            if (kvp.Value < 0.5)
            {
                bottlenecks.Add($"Struggles with {kvp.Key} tasks: {kvp.Value:P0} success rate");
            }
        }

        return Task.FromResult(bottlenecks);
    }

    private Task<List<string>> GenerateRecommendationsAsync(
        List<LearningEpisode> episodes,
        double successRate,
        double learningSpeedTrend,
        CancellationToken ct)
    {
        List<string> recommendations = new List<string>();

        if (successRate < 0.7)
        {
            recommendations.Add("Focus on improving learning quality over quantity");
            recommendations.Add("Consider using curriculum learning to build up from simpler tasks");
        }

        if (learningSpeedTrend < 0)
        {
            recommendations.Add("Learning speed is declining - review recent strategy changes");
        }
        else if (learningSpeedTrend > 0.2)
        {
            recommendations.Add("Learning speed is improving - continue current approach");
        }

        double avgExamples = episodes.Average(e => e.ExamplesProvided);
        if (avgExamples < 5)
        {
            recommendations.Add("Consider providing more examples for better learning");
        }

        return Task.FromResult(recommendations);
    }

    private List<MetaKnowledge> ParseMetaKnowledgeResponse(
        string response,
        string taskType,
        int supportingExamples)
    {
        List<MetaKnowledge> knowledge = new List<MetaKnowledge>();
        string[] lines = response.Split('\n');

        string? currentInsight = null;
        double currentConfidence = 0.7;
        List<string> currentApplicableTo = new List<string> { taskType };

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith("INSIGHT:", StringComparison.OrdinalIgnoreCase))
            {
                if (currentInsight != null)
                {
                    knowledge.Add(new MetaKnowledge(
                        Domain: taskType,
                        Insight: currentInsight,
                        Confidence: currentConfidence,
                        SupportingExamples: supportingExamples,
                        ApplicableTaskTypes: currentApplicableTo,
                        DiscoveredAt: DateTime.UtcNow));
                }

                currentInsight = trimmed.Substring("INSIGHT:".Length).Trim();
                currentConfidence = 0.7;
                currentApplicableTo = new List<string> { taskType };
            }
            else if (trimmed.StartsWith("CONFIDENCE:", StringComparison.OrdinalIgnoreCase))
            {
                string confStr = trimmed.Substring("CONFIDENCE:".Length).Trim();
                if (double.TryParse(confStr, out double conf))
                {
                    currentConfidence = conf;
                }
            }
            else if (trimmed.StartsWith("APPLICABLE_TO:", StringComparison.OrdinalIgnoreCase))
            {
                string applicableStr = trimmed.Substring("APPLICABLE_TO:".Length).Trim();
                currentApplicableTo = applicableStr.Split(',').Select(t => t.Trim()).ToList();
            }
        }

        if (currentInsight != null)
        {
            knowledge.Add(new MetaKnowledge(
                Domain: taskType,
                Insight: currentInsight,
                Confidence: currentConfidence,
                SupportingExamples: supportingExamples,
                ApplicableTaskTypes: currentApplicableTo,
                DiscoveredAt: DateTime.UtcNow));
        }

        return knowledge;
    }
}
