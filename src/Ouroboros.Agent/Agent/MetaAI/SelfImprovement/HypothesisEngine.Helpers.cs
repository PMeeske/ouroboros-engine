#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Hypothesis Engine - Private Helper Methods
// Prompt building, parsing, and analysis helpers
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Private helper methods for HypothesisEngine.
/// </summary>
public sealed partial class HypothesisEngine
{
    private string BuildHypothesisPrompt(
        string observation,
        List<Experience> experiences,
        Dictionary<string, object>? context)
    {
        string contextText = context != null && context.Any()
            ? $"\nContext: {JsonSerializer.Serialize(context)}"
            : "";

        string experienceText = experiences.Any()
            ? $"\nRelevant Past Experiences:\n{string.Join("\n", experiences.Take(3).Select(e => $"- {e.Goal}: {(e.Verification.Verified ? "Success" : "Failed")}"))}"
            : "";

        return $@"Generate a hypothesis to explain this observation:

Observation: {observation}{contextText}{experienceText}

Provide:
1. A clear hypothesis statement
2. Confidence level (0-1)
3. Domain/category
4. Initial supporting evidence

Format:
HYPOTHESIS: [statement]
CONFIDENCE: [0-1]
DOMAIN: [domain]
EVIDENCE: [supporting points]";
    }

    private Hypothesis ParseHypothesis(string response, string observation, Dictionary<string, object>? context)
    {
        string[] lines = response.Split('\n');

        string statement = observation;
        double confidence = 0.5;
        string domain = "general";
        List<string> supportingEvidence = new List<string>();

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith("HYPOTHESIS:", StringComparison.OrdinalIgnoreCase))
            {
                statement = trimmed.Substring("HYPOTHESIS:".Length).Trim();
            }
            else if (trimmed.StartsWith("CONFIDENCE:", StringComparison.OrdinalIgnoreCase))
            {
                string confStr = trimmed.Substring("CONFIDENCE:".Length).Trim();
                if (double.TryParse(confStr, out double conf))
                {
                    confidence = Math.Clamp(conf, 0.0, 1.0);
                }
            }
            else if (trimmed.StartsWith("DOMAIN:", StringComparison.OrdinalIgnoreCase))
            {
                domain = trimmed.Substring("DOMAIN:".Length).Trim();
            }
            else if (trimmed.StartsWith("EVIDENCE:", StringComparison.OrdinalIgnoreCase))
            {
                string evidence = trimmed.Substring("EVIDENCE:".Length).Trim();
                supportingEvidence.Add(evidence);
            }
        }

        return new Hypothesis(
            Guid.NewGuid(),
            statement,
            domain,
            confidence,
            supportingEvidence,
            new List<string>(),
            DateTime.UtcNow,
            Tested: false,
            Validated: null);
    }

    private List<PlanStep> ParseExperimentSteps(string response)
    {
        List<PlanStep> steps = new List<PlanStep>();
        string[] lines = response.Split('\n');

        string? currentAction = null;

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith("STEP"))
            {
                if (currentAction != null)
                {
                    steps.Add(new PlanStep(
                        currentAction,
                        new Dictionary<string, object>(),
                        "",
                        0.8));
                }

                currentAction = trimmed.Split(':').Skip(1).FirstOrDefault()?.Trim() ?? "";
            }
        }

        if (currentAction != null)
        {
            steps.Add(new PlanStep(
                currentAction,
                new Dictionary<string, object>(),
                "",
                0.8));
        }

        return steps;
    }

    private Dictionary<string, object> ParseExpectedOutcomes(string response)
    {
        Dictionary<string, object> outcomes = new Dictionary<string, object>();
        string[] lines = response.Split('\n');

        foreach (string line in lines)
        {
            if (line.Contains("EXPECTED_IF_TRUE:", StringComparison.OrdinalIgnoreCase))
            {
                outcomes["if_true"] = line.Split(':').Skip(1).FirstOrDefault()?.Trim() ?? "";
            }
            else if (line.Contains("EXPECTED_IF_FALSE:", StringComparison.OrdinalIgnoreCase))
            {
                outcomes["if_false"] = line.Split(':').Skip(1).FirstOrDefault()?.Trim() ?? "";
            }
            else if (line.Contains("CRITERIA:", StringComparison.OrdinalIgnoreCase))
            {
                outcomes["criteria"] = line.Split(':').Skip(1).FirstOrDefault()?.Trim() ?? "";
            }
        }

        return outcomes;
    }

    private bool AnalyzeExperimentResults(PlanExecutionResult execution, Dictionary<string, object> expectedOutcomes)
    {
        // Check if execution was successful
        if (!execution.Success)
            return false;

        // Check if there are any step results to analyze
        if (execution.StepResults.Count == 0)
            return false;

        // Simple heuristic: if most steps succeeded, hypothesis is likely supported
        double successRate = execution.StepResults.Count(r => r.Success) / (double)execution.StepResults.Count;

        return successRate >= 0.7;
    }

    [Obsolete("This method uses additive confidence adjustment. Use BayesianConfidence.Update() instead.")]
    private double CalculateConfidenceAdjustment(PlanExecutionResult execution, bool supported)
    {
        // Handle empty step results
        if (execution.StepResults.Count == 0)
            return supported ? 0.05 : -0.05;

        double successRate = execution.StepResults.Count(r => r.Success) / (double)execution.StepResults.Count;

        if (supported)
        {
            // Increase confidence based on how clean the success was
            return 0.1 + (successRate - 0.7) * 0.2;
        }
        else
        {
            // Decrease confidence
            return -0.15 - ((1.0 - successRate) * 0.1);
        }
    }

    /// <summary>
    /// Adjusts likelihood values based on execution quality.
    /// Low quality execution makes likelihoods move toward 0.5 (uninformative).
    /// </summary>
    /// <param name="baseLikelihood">The base likelihood to adjust</param>
    /// <param name="quality">Quality factor (0-1) based on execution success</param>
    /// <returns>Adjusted likelihood</returns>
    private static double AdjustLikelihoodByQuality(double baseLikelihood, double quality)
    {
        // Low quality execution -> likelihoods move toward 0.5 (uninformative)
        return baseLikelihood + (0.5 - baseLikelihood) * (1.0 - quality);
    }

    private string GenerateExplanation(Hypothesis hypothesis, PlanExecutionResult execution, bool supported)
    {
        if (supported)
        {
            return $"Experiment supports the hypothesis. " +
                   $"{execution.StepResults.Count(r => r.Success)}/{execution.StepResults.Count} steps succeeded.";
        }
        else
        {
            return $"Experiment does not support the hypothesis. " +
                   $"Only {execution.StepResults.Count(r => r.Success)}/{execution.StepResults.Count} steps succeeded.";
        }
    }
}
