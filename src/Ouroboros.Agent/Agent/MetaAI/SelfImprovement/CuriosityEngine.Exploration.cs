// ==========================================================
// Curiosity Engine - Exploration Helpers
// Opportunity identification, similarity calculation, and parsing
// ==========================================================

using System.Collections.Concurrent;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Partial class containing exploration opportunity identification,
/// action similarity calculation, experience retrieval, and step parsing helpers.
/// </summary>
public sealed partial class CuriosityEngine
{
    /// <summary>
    /// Identifies novel exploration opportunities.
    /// </summary>
    public async Task<List<ExplorationOpportunity>> IdentifyExplorationOpportunitiesAsync(
        int maxOpportunities = 5,
        CancellationToken ct = default)
    {
        List<ExplorationOpportunity> opportunities = new List<ExplorationOpportunity>();

        try
        {
            // Analyze what hasn't been explored
            IReadOnlyList<Skill> allSkills = _skills.GetAllSkills().ToSkills();
            List<Experience> experiences = await GetAllExperiences(ct);

            string prompt = $@"Identify unexplored areas for learning:

Current Skills ({allSkills.Count}):
{string.Join("\n", allSkills.Take(10).Select(s => $"- {s.Name}: {s.Description}"))}

Recent Experience Domains:
{string.Join("\n", experiences.Take(10).Select(e => $"- {e.Goal}"))}

Suggest {maxOpportunities} novel exploration areas that:
1. Differ from current capabilities
2. Could expand the agent's knowledge
3. Are safe to explore
4. Have potential for learning

Format each as:
OPPORTUNITY: [description]
NOVELTY: [0-1]
INFO_GAIN: [0-1]
";

            string response = await _llm.GenerateTextAsync(prompt, ct);

            // Parse opportunities
            string[] lines = response.Split('\n');
            string? description = null;
            double novelty = 0.7;
            double infoGain = 0.6;

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("OPPORTUNITY:", StringComparison.OrdinalIgnoreCase))
                {
                    if (description != null)
                    {
                        opportunities.Add(new ExplorationOpportunity(
                            description,
                            novelty,
                            infoGain,
                            new List<string>(),
                            DateTime.UtcNow));
                    }

                    description = trimmed.Substring("OPPORTUNITY:".Length).Trim();
                    novelty = 0.7;
                    infoGain = 0.6;
                }
                else if (trimmed.StartsWith("NOVELTY:", StringComparison.OrdinalIgnoreCase))
                {
                    string novStr = trimmed.Substring("NOVELTY:".Length).Trim();
                    if (double.TryParse(novStr, out double nov))
                        novelty = Math.Clamp(nov, 0.0, 1.0);
                }
                else if (trimmed.StartsWith("INFO_GAIN:", StringComparison.OrdinalIgnoreCase))
                {
                    string gainStr = trimmed.Substring("INFO_GAIN:".Length).Trim();
                    if (double.TryParse(gainStr, out double gain))
                        infoGain = Math.Clamp(gain, 0.0, 1.0);
                }
            }

            if (description != null)
            {
                opportunities.Add(new ExplorationOpportunity(
                    description,
                    novelty,
                    infoGain,
                    new List<string>(),
                    DateTime.UtcNow));
            }
        }
        catch
        {
            // Return empty list on error
        }

        return opportunities.Take(maxOpportunities).ToList();
    }

    // Private helper methods

    private double CalculateActionSimilarity(Plan plan1, Plan plan2)
    {
        if (plan1.Steps.Count == 0 || plan2.Steps.Count == 0)
            return 0.0;

        HashSet<string> actions1 = plan1.Steps.Select(s => s.Action).ToHashSet();
        HashSet<string> actions2 = plan2.Steps.Select(s => s.Action).ToHashSet();

        int intersection = actions1.Intersect(actions2).Count();
        int union = actions1.Union(actions2).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }

    private async Task<List<Experience>> GetAllExperiences(CancellationToken ct)
    {
        MemoryQuery query = new MemoryQuery(
            Tags: null,
            ContextSimilarity: null,
            SuccessOnly: null,
            FromDate: null,
            ToDate: null,
            MaxResults: 100);

        var result = await _memory.QueryExperiencesAsync(query, ct);

        if (!result.IsSuccess)
        {
            // If the memory query fails, return an empty list to avoid propagating errors.
            return new List<Experience>();
        }

        return result.Value.ToList();
    }

    private List<PlanStep> ParseExploratorySteps(string response)
    {
        List<PlanStep> steps = new List<PlanStep>();
        string[] lines = response.Split('\n');

        string? currentAction = null;
        string? currentExpected = null;
        double currentConfidence = 0.7;

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith("STEP"))
            {
                if (currentAction != null)
                {
                    steps.Add(new PlanStep(
                        currentAction,
                        new Dictionary<string, object> { ["expected_learning"] = currentExpected ?? "" },
                        currentExpected ?? "",
                        currentConfidence));
                }

                currentAction = trimmed.Split(':').Skip(1).FirstOrDefault()?.Trim() ?? "";
                currentExpected = "";
                currentConfidence = 0.7;
            }
            else if (trimmed.StartsWith("EXPECTED:", StringComparison.OrdinalIgnoreCase))
            {
                currentExpected = trimmed.Substring("EXPECTED:".Length).Trim();
            }
            else if (trimmed.StartsWith("CONFIDENCE:", StringComparison.OrdinalIgnoreCase))
            {
                string confStr = trimmed.Substring("CONFIDENCE:".Length).Trim();
                if (double.TryParse(confStr, out double conf))
                {
                    currentConfidence = Math.Clamp(conf, 0.0, 1.0);
                }
            }
        }

        if (currentAction != null)
        {
            steps.Add(new PlanStep(
                currentAction,
                new Dictionary<string, object> { ["expected_learning"] = currentExpected ?? "" },
                currentExpected ?? "",
                currentConfidence));
        }

        return steps;
    }
}
