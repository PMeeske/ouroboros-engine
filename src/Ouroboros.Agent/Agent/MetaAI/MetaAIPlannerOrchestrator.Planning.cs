using Ouroboros.Core.Ethics;
using Ouroboros.Pipeline.Prompts;

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
            List<Experience> pastExperiences = await _memory.RetrieveRelevantExperiencesAsync(query, ct).ConfigureAwait(false);

            // Find matching skills
            List<Skill> matchingSkills = await _skills.FindMatchingSkillsAsync(goal, context, ct).ConfigureAwait(false);

            // Generate plan using LLM with past experience and skills
            string planPrompt = BuildPlanPrompt(goal, context, pastExperiences, matchingSkills);
            string planText = await _llm.GenerateTextAsync(planPrompt, ct).ConfigureAwait(false);

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

            var ethicsResult = await _ethics.EvaluatePlanAsync(planContext, ct).ConfigureAwait(false);

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

                var approvalResponse = await _approvalProvider.RequestApprovalAsync(approvalRequest, ct).ConfigureAwait(false);

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
        catch (Exception ex) when (ex is not OperationCanceledException)
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
        string contextSection = context != null && context.Any()
            ? $"CONTEXT: {JsonSerializer.Serialize(context)}\n"
            : string.Empty;

        string skillsSection = string.Empty;
        if (matchingSkills.Any())
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("AVAILABLE SKILLS:\n");
            foreach (Skill? skill in matchingSkills.Take(3))
            {
                sb.Append($"- {skill.Name}: {skill.Description} (Success rate: {skill.SuccessRate:P0})\n");
            }
            skillsSection = sb.ToString();
        }

        string experienceSection = string.Empty;
        if (pastExperiences.Any())
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("PAST EXPERIENCE:\n");
            foreach (Experience? exp in pastExperiences.Take(3))
            {
                sb.Append($"- Goal: {exp.Goal}, Quality: {exp.Verification.QualityScore:P0}, Verified: {exp.Verification.Verified}\n");
            }
            experienceSection = sb.ToString();
        }

        string toolsText = string.Join("\n", _tools.All.Select(t => $"- {t.Name}: {t.Description}"));

        return PromptTemplateLoader.GetPromptText("MetaAI", "Planner")
            .Replace("{{$goal}}", goal)
            .Replace("{{$context}}", contextSection)
            .Replace("{{$skills}}", skillsSection)
            .Replace("{{$experience}}", experienceSection)
            .Replace("{{$tools}}", toolsText);
    }

    private static Plan ParsePlan(string planText, string goal)
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
                catch (Exception ex) when (ex is not OperationCanceledException) {
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
