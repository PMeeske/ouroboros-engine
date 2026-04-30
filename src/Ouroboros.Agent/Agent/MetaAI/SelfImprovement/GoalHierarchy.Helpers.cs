
namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Partial class containing parsing, conflict detection, and constraint helpers.
/// </summary>
public sealed partial class GoalHierarchy
{
    private List<Goal> ParseSubgoals(string response, Goal parentGoal)
    {
        List<Goal> subgoals = new List<Goal>();
        string[] lines = response.Split('\n');

        string? description = null;
        GoalType type = GoalType.Instrumental;
        double priority = 0.5;

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith("SUBGOAL"))
            {
                if (description != null)
                {
                    Goal subgoal = new Goal(
                        Guid.NewGuid(),
                        description,
                        type,
                        priority,
                        parentGoal,
                        new List<Goal>(),
                        new Dictionary<string, object>(),
                        DateTime.UtcNow,
                        false,
                        null);
                    subgoals.Add(subgoal);
                }

                description = trimmed.Split(':').Skip(1).FirstOrDefault()?.Trim() ?? "";
                type = GoalType.Instrumental;
                priority = 0.5;
            }
            else if (trimmed.StartsWith("TYPE:"))
            {
                string typeStr = trimmed.Substring("TYPE:".Length).Trim();
                type = Enum.TryParse<GoalType>(typeStr, true, out GoalType parsed)
                    ? parsed
                    : GoalType.Instrumental;
            }
            else if (trimmed.StartsWith("PRIORITY:"))
            {
                string priorityStr = trimmed.Substring("PRIORITY:".Length).Trim();
                if (double.TryParse(priorityStr, out double p))
                {
                    priority = Math.Clamp(p, 0.0, 1.0);
                }
            }
        }

        if (description != null)
        {
            Goal subgoal = new Goal(
                Guid.NewGuid(),
                description,
                type,
                priority,
                parentGoal,
                new List<Goal>(),
                new Dictionary<string, object>(),
                DateTime.UtcNow,
                false,
                null);
            subgoals.Add(subgoal);
        }

        return subgoals.Take(_config.MaxSubgoalsPerGoal).ToList();
    }

    private static bool IsComplexGoal(Goal goal)
    {
        // Goals with lower priority or instrumental type are less complex
        return goal.Priority > 0.7 || goal.Type == GoalType.Primary;
    }

    private static bool HasDirectConflict(Goal goal1, Goal goal2)
    {
        // Check constraint conflicts
        foreach (KeyValuePair<string, object> constraint1 in goal1.Constraints)
        {
            if (goal2.Constraints.TryGetValue(constraint1.Key, out object? value2) &&
                !constraint1.Value.Equals(value2))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasResourceConflict(Goal goal1, Goal goal2)
    {
        // Simple heuristic: high-priority goals may compete
        return goal1.Priority > 0.8 && goal2.Priority > 0.8 &&
               goal1.Type == goal2.Type;
    }

    private async Task<GoalConflict?> DetectSemanticConflictAsync(
        Goal goal1,
        Goal goal2,
        CancellationToken ct)
    {
        string prompt = $@"Analyze if these two goals conflict:

GOAL 1: {goal1.Description} (Type: {goal1.Type}, Priority: {goal1.Priority})
GOAL 2: {goal2.Description} (Type: {goal2.Type}, Priority: {goal2.Priority})

Do they conflict? Answer 'YES' or 'NO' and explain why.";

        try
        {
            string response = await _llm.GenerateTextAsync(prompt, ct).ConfigureAwait(false);

            if (response.Contains("YES", StringComparison.OrdinalIgnoreCase))
            {
                return new GoalConflict(
                    goal1,
                    goal2,
                    "Semantic conflict",
                    response,
                    new List<string> { "Reframe goals", "Adjust priorities", "Create intermediate goals" });
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            // Ignore LLM errors in conflict detection
        }

        return null;
    }

    private async Task<bool> CheckConstraintViolationAsync(
        Goal goal,
        string constraint,
        CancellationToken ct)
    {
        string prompt = $@"Does this goal violate the safety constraint?

GOAL: {goal.Description}
CONSTRAINT: {constraint}

Answer with 'VIOLATES' or 'SAFE'.";

        try
        {
            string response = await _llm.GenerateTextAsync(prompt, ct).ConfigureAwait(false);
            return response.Contains("VIOLATES", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            // On error, assume safe to not block execution
            return false;
        }
    }
}
