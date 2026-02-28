
using System.Text.Json;
using System.Text.RegularExpressions;
using Ouroboros.Core.Ethics;

namespace Ouroboros.Agent.MetaAI;

public sealed partial class MetaAIPlannerOrchestrator
{
    /// <summary>
    /// Plans how to accomplish a goal using available skills and past experience.
    /// </summary>
    public async Task<Result<Plan, string>> PlanAsync(
        string goal,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(goal))
            return Result<Plan, string>.Failure("Goal cannot be empty");

        try
        {
                        // Check if we have relevant past experiences
            MemoryQuery query = MemoryQueryExtensions.ForGoal(goal, context, maxResults: 5, minSimilarity: 0.7);
            var experiencesResult = await _memory.RetrieveRelevantExperiencesAsync(query, ct);
            List<Experience> pastExperiences = experiencesResult.IsSuccess ? experiencesResult.Value.ToList() : new List<Experience>();

            // Find matching skills
            List<Skill> matchingSkills = await _skills.FindMatchingSkillsAsync(goal, context);

            // Generate plan using LLM with past experience and skills
            string planPrompt = BuildPlanPrompt(goal, context, pastExperiences, matchingSkills);
            string planText = await _llm.GenerateTextAsync(planPrompt, ct);

            // Parse plan from LLM response
            Plan plan = ParsePlan(planText, goal);

            // Ethics evaluation - foundational gate that runs BEFORE safety checks
            // This ensures ethical constraints are the first and primary consideration
            var planContext = new PlanContext
            {
                Plan = new Core.Ethics.Plan
                {
                    Goal = goal,
                    Steps = plan.Steps.Select(s => new Core.Ethics.PlanStep
                    {
                        Action = s.Action,
                        Parameters = s.Parameters,
                        ExpectedOutcome = s.ExpectedOutcome,
                        ConfidenceScore = s.ConfidenceScore
                    }).ToArray(),
                    ConfidenceScores = plan.ConfidenceScores,
                    CreatedAt = plan.CreatedAt
                },
                ActionContext = new ActionContext
                {
                    AgentId = "meta-ai-planner",
                    UserId = null, // Set if available from context
                    Environment = context?.ContainsKey("environment") == true
                        ? context["environment"]?.ToString() ?? "planning"
                        : "planning",
                    State = context != null
                        ? new Dictionary<string, object>(context)
                        : new Dictionary<string, object>()
                }
            };

            var ethicsResult = await _ethics.EvaluatePlanAsync(planContext, ct);

            if (ethicsResult.IsFailure)
            {
                return Result<Plan, string>.Failure(
                    $"Plan failed ethics evaluation: {ethicsResult.Error}");
            }

            if (!ethicsResult.Value.IsPermitted)
            {
                return Result<Plan, string>.Failure(
                    $"Plan blocked by ethics framework: {ethicsResult.Value.Reasoning}");
            }

            if (ethicsResult.Value.Level == EthicalClearanceLevel.RequiresHumanApproval)
            {
                var approvalRequest = new HumanApprovalRequest
                {
                    Category = "plan",
                    Description = $"Plan for goal: {goal}",
                    Clearance = ethicsResult.Value,
                    Context = new Dictionary<string, object>
                    {
                        ["goal"] = goal,
                        ["steps"] = plan.Steps.Select(s => s.Action).ToList(),
                        ["concerns"] = ethicsResult.Value.Concerns.Select(c => c.Description).ToList(),
                        ["reasoning"] = ethicsResult.Value.Reasoning
                    }
                };

                var approvalResponse = await _approvalProvider.RequestApprovalAsync(approvalRequest, ct);

                if (approvalResponse.Decision != HumanApprovalDecision.Approved)
                {
                    return Result<Plan, string>.Failure(
                        $"Plan requires human approval and was {approvalResponse.Decision}: " +
                        $"{approvalResponse.ReviewerComments ?? ethicsResult.Value.Reasoning}");
                }
            }

            // Safety checks - secondary validation after ethics clearance
            foreach (PlanStep step in plan.Steps)
            {
                SafetyCheckResult safetyCheck = _safety.CheckSafety(
                    step.Action,
                    step.Parameters,
                    PermissionLevel.UserDataWithConfirmation);

                if (!safetyCheck.Safe)
                {
                    return Result<Plan, string>.Failure(
                        $"Plan step '{step.Action}' failed safety check: {string.Join(", ", safetyCheck.Violations)}");
                }
            }

            RecordMetric("planner", 1.0, true);
            return Result<Plan, string>.Success(plan);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            RecordMetric("planner", 1.0, false);
            return Result<Plan, string>.Failure($"Planning failed: {ex.Message}");
        }
    }

    private string BuildPlanPrompt(
        string goal,
        Dictionary<string, object>? context,
        List<Experience> pastExperiences,
        List<Skill> matchingSkills)
    {
        string prompt = $@"You are an AI planner. Create a detailed plan to accomplish this goal:

GOAL: {goal}

";

        if (context != null && context.Any())
        {
            prompt += $"CONTEXT: {JsonSerializer.Serialize(context)}\n\n";
        }

        if (matchingSkills.Any())
        {
            prompt += "AVAILABLE SKILLS:\n";
            foreach (Skill? skill in matchingSkills.Take(3))
            {
                prompt += $"- {skill.Name}: {skill.Description} (Success rate: {skill.SuccessRate:P0})\n";
            }
            prompt += "\n";
        }

        if (pastExperiences.Any())
        {
            prompt += "PAST EXPERIENCE:\n";
            foreach (Experience? exp in pastExperiences.Take(3))
            {
                prompt += $"- Goal: {exp.Goal}, Quality: {exp.Verification.QualityScore:P0}, Verified: {exp.Verification.Verified}\n";
            }
            prompt += "\n";
        }

        prompt += $@"AVAILABLE TOOLS:
{string.Join("\n", _tools.All.Select(t => $"- {t.Name}: {t.Description}"))}

Create a plan with specific steps. For each step, specify:
1. Action (tool name or task description)
2. Parameters (as JSON object with ACTUAL CONCRETE VALUES)
3. Expected outcome
4. Confidence score (0-1)

CRITICAL PARAMETER RULES:
- Use ACTUAL CONCRETE VALUES in parameters - never placeholder descriptions
- URLs must be real URLs (https://example.com), not descriptions like 'URL from search'
- Search queries must be actual text, not 'query about topic'
- If you don't have a real value yet, SKIP that step or mark confidence as 0

WRONG parameters: {{""url"": ""URL of the search result"", ""query"": ""search for topic""}}
CORRECT parameters: {{""url"": ""https://example.com/page"", ""query"": ""Ouroboros mythology serpent""}}

Format your response as:
STEP 1: [action]
PARAMETERS: {{...}}
EXPECTED: [outcome]
CONFIDENCE: [0-1]

STEP 2: ...
";

        return prompt;
    }

    private Plan ParsePlan(string planText, string goal)
    {
        List<PlanStep> steps = new List<PlanStep>();
        Dictionary<string, double> confidenceScores = new Dictionary<string, double>();

        // Simple parsing - in production, use more robust parser
        string[] lines = planText.Split('\n');
        string? currentAction = null;
        Dictionary<string, object>? currentParams = null;
        string? currentExpected = null;
        double currentConfidence = 0.8;

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith("STEP"))
            {
                if (currentAction != null)
                {
                    steps.Add(new PlanStep(currentAction, currentParams ?? new(), currentExpected ?? "", currentConfidence));
                }

                currentAction = trimmed.Split(':').Skip(1).FirstOrDefault()?.Trim() ?? "";
                currentParams = new();
                currentExpected = "";
                currentConfidence = 0.8;
            }
            else if (trimmed.StartsWith("PARAMETERS:"))
            {
                string paramsJson = trimmed.Substring("PARAMETERS:".Length).Trim();
                try
                {
                    currentParams = JsonSerializer.Deserialize<Dictionary<string, object>>(paramsJson) ?? new();
                }
                catch
                {
                    currentParams = new Dictionary<string, object> { ["raw"] = paramsJson };
                }
            }
            else if (trimmed.StartsWith("EXPECTED:"))
            {
                currentExpected = trimmed.Substring("EXPECTED:".Length).Trim();
            }
            else if (trimmed.StartsWith("CONFIDENCE:"))
            {
                string confStr = trimmed.Substring("CONFIDENCE:".Length).Trim();
                if (double.TryParse(confStr, out double conf))
                {
                    currentConfidence = conf;
                }
            }
        }

        if (currentAction != null)
        {
            steps.Add(new PlanStep(currentAction, currentParams ?? new(), currentExpected ?? "", currentConfidence));
        }

        // If no steps were parsed, create a simple default plan
        if (steps.Count == 0)
        {
            steps.Add(new PlanStep(
                "llm_direct",
                new Dictionary<string, object> { ["goal"] = goal },
                "Direct LLM response",
                0.5));
        }

        confidenceScores["overall"] = steps.Any() ? steps.Average(s => s.ConfidenceScore) : 0.5;

        return new Plan(goal, steps, confidenceScores, DateTime.UtcNow);
    }
}
