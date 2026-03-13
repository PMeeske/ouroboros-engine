using System.Text.Json;
using Ouroboros.Abstractions.Core;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Cognition.Planning;

/// <summary>
/// Goal splitter powered by Semantic Kernel planning and Hypergrid routing.
/// Implements the PEV (Plan-Evaluate-Verify) pattern from Whitepaper Section 5.2.
///
/// Plan:    SK planner decomposes the goal into raw steps.
/// Evaluate: HypergridRouter assigns dimensional coordinates.
/// Verify:  Ethics framework validates each step.
/// </summary>
public sealed class SemanticKernelGoalSplitter : IGoalSplitter
{
    private readonly IChatCompletionModel _llm;
    private readonly HypergridRouter _router;
    private readonly Core.Ethics.IEthicsFramework? _ethics;
    private readonly GoalSplitterConfig _config;

    public SemanticKernelGoalSplitter(
        IChatCompletionModel llm,
        HypergridRouter? router = null,
        Core.Ethics.IEthicsFramework? ethics = null,
        GoalSplitterConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(llm);
        _llm = llm;
        _router = router ?? new HypergridRouter();
        _ethics = ethics;
        _config = config ?? new GoalSplitterConfig();
    }

    public async Task<Result<GoalDecomposition, string>> SplitAsync(
        Goal goal,
        HypergridContext context,
        CancellationToken ct = default)
    {
        try
        {
            // Phase 1: PLAN — Use LLM to decompose into raw steps
            var rawSteps = await PlanAsync(goal, context, ct);
            if (rawSteps.Count == 0)
                return Result<GoalDecomposition, string>.Failure("Planner produced no steps");

            // Phase 2: EVALUATE — Route through Hypergrid dimensions
            var (steps, analysis) = _router.Route(rawSteps, context);

            // Phase 3: VERIFY — Ethics check on high-risk steps
            if (_ethics is not null)
            {
                foreach (var step in steps.Where(s => s.Mode == ExecutionMode.RequiresApproval))
                {
                    var ethicsGoal = new Core.Ethics.Goal
                    {
                        Id = step.Id,
                        Description = step.Description,
                        Type = step.Type.ToString(),
                        Priority = step.Priority
                    };
                    var ethicsContext = new Core.Ethics.ActionContext
                    {
                        AgentId = "goal-splitter",
                        Environment = "planning",
                        State = new Dictionary<string, object>
                        {
                            ["modal"] = step.Coordinate.Modal,
                            ["executionMode"] = step.Mode.ToString()
                        }
                    };

                    var result = await _ethics.EvaluateGoalAsync(ethicsGoal, ethicsContext, ct);
                    if (result.IsSuccess && !result.Value.IsPermitted)
                    {
                        return Result<GoalDecomposition, string>.Failure(
                            $"Step '{step.Description}' rejected by ethics: {result.Value.Reasoning}");
                    }
                }
            }

            var decomposition = new GoalDecomposition(goal, steps, analysis);
            return Result<GoalDecomposition, string>.Success(decomposition);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Result<GoalDecomposition, string>.Failure(
                $"Goal splitting failed: {ex.Message}");
        }
    }

    private async Task<IReadOnlyList<RawGoalStep>> PlanAsync(
        Goal goal, HypergridContext context, CancellationToken ct)
    {
        string skillsHint = context.AvailableSkills.Count > 0
            ? $"\nAvailable skills: {string.Join(", ", context.AvailableSkills)}"
            : "";
        string toolsHint = context.AvailableTools.Count > 0
            ? $"\nAvailable tools: {string.Join(", ", context.AvailableTools)}"
            : "";
        string deadlineHint = context.Deadline is { } dl
            ? $"\nDeadline: {dl:u} ({(dl - DateTimeOffset.UtcNow).TotalHours:F1}h remaining)"
            : "";

        string prompt = $$"""
            Decompose this goal into {{_config.MaxSteps}} or fewer concrete, actionable steps.
            Return ONLY valid JSON — no markdown, no commentary.

            GOAL: {{goal.Description}}
            TYPE: {{goal.Type}}
            PRIORITY: {{goal.Priority}}{{skillsHint}}{{toolsHint}}{{deadlineHint}}

            For each step analyze:
            - What needs to be done (description)
            - Type: Primary, Secondary, Instrumental, or Safety
            - Priority: 0.0 to 1.0
            - Dependencies: which step indices (0-based) must complete first

            Return JSON array:
            [
              {"description": "...", "type": "Instrumental", "priority": 0.8, "dependsOn": []},
              {"description": "...", "type": "Primary", "priority": 0.9, "dependsOn": [0]}
            ]
            """;

        string response = await _llm.GenerateTextAsync(prompt, ct);
        return ParseSteps(response);
    }

    private IReadOnlyList<RawGoalStep> ParseSteps(string response)
    {
        // Extract JSON array from response (handle markdown fences)
        string json = response.Trim();
        int arrayStart = json.IndexOf('[');
        int arrayEnd = json.LastIndexOf(']');
        if (arrayStart < 0 || arrayEnd < 0 || arrayEnd <= arrayStart)
            return Array.Empty<RawGoalStep>();

        json = json[arrayStart..(arrayEnd + 1)];

        try
        {
            var items = JsonSerializer.Deserialize<List<StepDto>>(json, s_jsonOptions);
            if (items is null) return Array.Empty<RawGoalStep>();

            // Two-pass: create steps first, then resolve dependencies
            var steps = new List<RawGoalStep>(items.Count);
            var idByIndex = new Dictionary<int, Guid>();

            for (int i = 0; i < items.Count && i < _config.MaxSteps; i++)
            {
                var id = Guid.NewGuid();
                idByIndex[i] = id;
                steps.Add(new RawGoalStep(id, items[i].Description ?? $"Step {i + 1}",
                    ParseGoalType(items[i].Type), Math.Clamp(items[i].Priority, 0, 1),
                    Array.Empty<Guid>()));
            }

            // Resolve dependency indices to Guids
            for (int i = 0; i < steps.Count; i++)
            {
                var deps = items[i].DependsOn?
                    .Where(idx => idx >= 0 && idx < steps.Count && idx != i)
                    .Select(idx => idByIndex[idx])
                    .ToArray() ?? Array.Empty<Guid>();

                if (deps.Length > 0)
                    steps[i] = steps[i] with { DependsOn = deps };
            }

            return steps;
        }
        catch (JsonException)
        {
            return Array.Empty<RawGoalStep>();
        }
    }

    private static GoalType ParseGoalType(string? type) => type?.ToLowerInvariant() switch
    {
        "primary" => GoalType.Primary,
        "secondary" => GoalType.Secondary,
        "instrumental" => GoalType.Instrumental,
        "safety" => GoalType.Safety,
        _ => GoalType.Instrumental
    };

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private sealed record StepDto(
        string? Description,
        string? Type,
        double Priority,
        int[]? DependsOn);
}

/// <summary>Configuration for the goal splitter.</summary>
public sealed record GoalSplitterConfig(int MaxSteps = 8, int MaxRetries = 2);
