#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Concurrent;
using System.Diagnostics;
using Ouroboros.Core.Hyperon;
using Ouroboros.Core.LawsOfForm;
using Unit = Ouroboros.Abstractions.Unit;

namespace Ouroboros.Agent.MetaAI;

public sealed partial class MeTTaOrchestrator
{
    // Helper methods
    private async Task<StepResult> ExecuteStepAsync(PlanStep step, CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();
        Dictionary<string, object> observedState = new Dictionary<string, object>();

        try
        {
            // Validate step action is not empty
            if (string.IsNullOrWhiteSpace(step.Action))
            {
                return new StepResult(
                    step, false, string.Empty,
                    "Step action/tool name cannot be empty",
                    sw.Elapsed, observedState);
            }

            // Find and execute the tool
            Option<ITool> toolOption = _tools.GetTool(step.Action);
            if (!toolOption.HasValue)
            {
                return new StepResult(
                    step, false, string.Empty,
                    $"Tool '{step.Action}' not found",
                    sw.Elapsed, observedState);
            }

            ITool tool = toolOption.Value!;
            string toolInput = JsonSerializer.Serialize(step.Parameters);
            Result<string, string> result = await tool.InvokeAsync(toolInput, ct);

            return result.Match(
                output => new StepResult(step, true, output, null, sw.Elapsed, observedState),
                error => new StepResult(step, false, string.Empty, error, sw.Elapsed, observedState)
            );
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new StepResult(step, false, string.Empty, ex.Message, sw.Elapsed, observedState);
        }
    }

    private string BuildPlanPrompt(
        string goal,
        Dictionary<string, object>? context,
        List<Experience> pastExperiences,
        List<Skill> matchingSkills)
    {
        string prompt = $"Create a detailed plan to accomplish: {goal}\n\n";

        if (context?.Any() == true)
        {
            prompt += "Context:\n";
            foreach (KeyValuePair<string, object> item in context)
                prompt += $"- {item.Key}: {item.Value}\n";
            prompt += "\n";
        }

        prompt += "Available tools:\n";
        foreach (ITool tool in _tools.All)
            prompt += $"- {tool.Name}: {tool.Description}\n";
        prompt += "\n";

        if (pastExperiences.Count > 0)
        {
            prompt += "Relevant past experiences:\n";
            foreach (Experience? exp in pastExperiences.Take(3))
                prompt += $"- {exp.Goal} (success: {exp.Verification.Verified}, quality: {exp.Verification.QualityScore:F2})\n";
            prompt += "\n";
        }

        prompt += @"Provide a plan as JSON array of steps:
[
  {
    ""action"": ""tool_name"",
    ""parameters"": {},
    ""expected_outcome"": ""what should happen"",
    ""confidence"": 0.9
  }
]

CRITICAL PARAMETER RULES:
- Use ACTUAL CONCRETE VALUES in parameters - never placeholder descriptions
- URLs must be real URLs (https://example.com), not descriptions like 'URL from search'
- Search queries must be actual text, not 'query about topic'
- If you don't have a real value, SKIP that step or ask for it

WRONG parameters: {""url"": ""URL of the search result"", ""query"": ""search for topic""}
CORRECT parameters: {""url"": ""https://example.com/page"", ""query"": ""Ouroboros mythology serpent""}";

        return prompt;
    }

    private Plan ParsePlan(string planText, string goal)
    {
        List<PlanStep> steps = new List<PlanStep>();
        Dictionary<string, double> confidenceScores = new Dictionary<string, double>();

        try
        {
            using JsonDocument doc = JsonDocument.Parse(planText);
            JsonElement array = doc.RootElement;

            if (array.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement element in array.EnumerateArray())
                {
                    string action = element.GetProperty("action").GetString() ?? "";
                    string expected = element.GetProperty("expected_outcome").GetString() ?? "";
                    double confidence = element.TryGetProperty("confidence", out JsonElement conf)
                        ? conf.GetDouble()
                        : 0.5;

                    Dictionary<string, object> parameters = new Dictionary<string, object>();
                    if (element.TryGetProperty("parameters", out JsonElement paramsElement))
                    {
                        foreach (JsonProperty prop in paramsElement.EnumerateObject())
                        {
                            parameters[prop.Name] = prop.Value.ToString();
                        }
                    }

                    steps.Add(new PlanStep(action, parameters, expected, confidence));
                }
            }
        }
        catch
        {
            // Fallback: create simple plan
            steps.Add(new PlanStep(
                "llm_direct",
                new Dictionary<string, object> { ["goal"] = goal },
                "Direct LLM response",
                0.5));
        }

        confidenceScores["overall"] = steps.Any() ? steps.Average(s => s.ConfidenceScore) : 0.5;
        return new Plan(goal, steps, confidenceScores, DateTime.UtcNow);
    }

    private string BuildVerificationPrompt(PlanExecutionResult execution)
    {
        string prompt = $"Verify the execution of plan: {execution.Plan.Goal}\n\n";
        prompt += $"Success: {execution.Success}\n";
        prompt += $"Duration: {execution.Duration.TotalSeconds:F2}s\n\n";

        prompt += "Steps executed:\n";
        foreach (StepResult result in execution.StepResults)
        {
            prompt += $"- {result.Step.Action}: {(result.Success ? "✓" : "✗")} {result.Output}\n";
        }

        prompt += "\nProvide verification in JSON format:\n";
        prompt += @"{
  ""verified"": true/false,
  ""quality_score"": 0.0-1.0,
  ""issues"": [""issue1"", ""issue2""],
  ""improvements"": [""suggestion1"", ""suggestion2""]
}";

        return prompt;
    }

    private PlanVerificationResult ParseVerification(PlanExecutionResult execution, string verificationText)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(verificationText);
            JsonElement root = doc.RootElement;

            bool verified = root.GetProperty("verified").GetBoolean();
            double qualityScore = root.GetProperty("quality_score").GetDouble();

            List<string> issues = new List<string>();
            if (root.TryGetProperty("issues", out JsonElement issuesArray))
            {
                foreach (JsonElement issue in issuesArray.EnumerateArray())
                {
                    issues.Add(issue.GetString() ?? "");
                }
            }

            List<string> improvements = new List<string>();
            if (root.TryGetProperty("improvements", out JsonElement improvArray))
            {
                foreach (JsonElement improvement in improvArray.EnumerateArray())
                {
                    improvements.Add(improvement.GetString() ?? "");
                }
            }

            return new PlanVerificationResult(execution, verified, qualityScore, issues, improvements, null);
        }
        catch
        {
            return new PlanVerificationResult(
                execution,
                execution.Success,
                execution.Success ? 0.7 : 0.3,
                new List<string>(),
                new List<string>(),
                null);
        }
    }

    private string FormatPlanForMeTTa(Plan plan)
    {
        string steps = string.Join(" ", plan.Steps.Select((s, i) => $"(step {i} {s.Action})"));
        return $"(plan {steps})";
    }

    private void RecordMetric(string component, double latencyMs, bool success)
    {
        _metrics.AddOrUpdate(
            component,
            _ => new PerformanceMetrics(
                ResourceName: component,
                ExecutionCount: 1,
                AverageLatencyMs: latencyMs,
                SuccessRate: success ? 1.0 : 0.0,
                LastUsed: DateTime.UtcNow,
                CustomMetrics: new Dictionary<string, double>()
            ),
            (_, old) =>
            {
                int totalCalls = old.ExecutionCount + 1;
                int successCalls = (int)(old.SuccessRate * old.ExecutionCount) + (success ? 1 : 0);
                double avgLatency = (old.AverageLatencyMs * old.ExecutionCount + latencyMs) / totalCalls;
                double successRate = (double)successCalls / totalCalls;

                return new PerformanceMetrics(
                    ResourceName: component,
                    ExecutionCount: totalCalls,
                    AverageLatencyMs: avgLatency,
                    SuccessRate: successRate,
                    LastUsed: DateTime.UtcNow,
                    CustomMetrics: old.CustomMetrics
                );
            });
    }
}
