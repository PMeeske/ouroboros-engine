// ==========================================================
// Goal Hierarchy Implementation
// Hierarchical goal decomposition with value alignment
// ==========================================================

using System.Collections.Concurrent;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Implementation of hierarchical goal management.
/// Decomposes complex goals and ensures value alignment.
/// </summary>
public sealed partial class GoalHierarchy : IGoalHierarchy
{
    private readonly ConcurrentDictionary<Guid, Goal> _goals = new();
    private readonly Ouroboros.Abstractions.Core.IChatCompletionModel _llm;
    private readonly ISafetyGuard _safety;
    private readonly Core.Ethics.IEthicsFramework _ethics;
    private readonly GoalHierarchyConfig _config;

    public GoalHierarchy(
        Ouroboros.Abstractions.Core.IChatCompletionModel llm,
        ISafetyGuard safety,
        Core.Ethics.IEthicsFramework ethics,
        GoalHierarchyConfig? config = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _safety = safety ?? throw new ArgumentNullException(nameof(safety));
        _ethics = ethics ?? throw new ArgumentNullException(nameof(ethics));
        _config = config ?? new GoalHierarchyConfig();
    }

    /// <summary>
    /// Adds a goal to the hierarchy.
    /// </summary>
    public void AddGoal(Goal goal)
    {
        if (goal == null)
            throw new ArgumentNullException(nameof(goal));

        _goals[goal.Id] = goal;

        // Add subgoals recursively
        foreach (Goal subgoal in goal.Subgoals)
        {
            AddGoal(subgoal);
        }
    }

    /// <summary>
    /// Adds a goal to the hierarchy with ethics evaluation.
    /// </summary>
    public async Task<Result<Goal, string>> AddGoalAsync(Goal goal, CancellationToken ct = default)
    {
        if (goal == null)
            return Result<Goal, string>.Failure("Goal cannot be null");

        try
        {
            // Ethics evaluation - validate goal before accepting
            var goalForEthics = new Core.Ethics.Goal
            {
                Id = goal.Id,
                Description = goal.Description,
                Type = goal.Type.ToString(),
                Priority = goal.Priority
            };

            var context = new Core.Ethics.ActionContext
            {
                AgentId = "goal-hierarchy",
                UserId = null,
                Environment = "goal-management",
                State = new Dictionary<string, object>
                {
                    ["goal_type"] = goal.Type.ToString(),
                    ["priority"] = goal.Priority,
                    ["has_constraints"] = goal.Constraints.Count > 0
                }
            };

            var ethicsResult = await _ethics.EvaluateGoalAsync(goalForEthics, context, ct);

            if (ethicsResult.IsFailure)
            {
                return Result<Goal, string>.Failure(
                    $"Goal rejected by ethics evaluation: {ethicsResult.Error}");
            }

            if (!ethicsResult.Value.IsPermitted)
            {
                return Result<Goal, string>.Failure(
                    $"Goal rejected by ethics framework: {ethicsResult.Value.Reasoning}");
            }

            if (ethicsResult.Value.Level == Core.Ethics.EthicalClearanceLevel.RequiresHumanApproval)
            {
                return Result<Goal, string>.Failure(
                    $"Goal requires human approval before acceptance: {ethicsResult.Value.Reasoning}");
            }

            // Add goal to hierarchy
            _goals[goal.Id] = goal;

            // Add subgoals recursively
            foreach (Goal subgoal in goal.Subgoals)
            {
                AddGoal(subgoal);
            }

            return Result<Goal, string>.Success(goal);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Result<Goal, string>.Failure($"Goal addition failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a goal by ID.
    /// </summary>
    public Goal? GetGoal(Guid id)
    {
        _goals.TryGetValue(id, out Goal? goal);
        return goal;
    }

    /// <summary>
    /// Gets all active goals (not completed).
    /// </summary>
    public List<Goal> GetActiveGoals()
    {
        return _goals.Values
            .Where(g => !g.IsComplete)
            .OrderByDescending(g => g.Priority)
            .ToList();
    }

    /// <summary>
    /// Decomposes a complex goal into subgoals.
    /// </summary>
    public async Task<Result<Goal, string>> DecomposeGoalAsync(
        Goal goal,
        int maxDepth = 3,
        CancellationToken ct = default)
    {
        if (goal == null)
            return Result<Goal, string>.Failure("Goal cannot be null");

        if (maxDepth <= 0)
            return Result<Goal, string>.Success(goal);

        try
        {
            // Use LLM to decompose the goal
            string prompt = $@"Decompose this goal into {_config.MaxSubgoalsPerGoal} or fewer specific, actionable subgoals:

GOAL: {goal.Description}
TYPE: {goal.Type}
PRIORITY: {goal.Priority}

Provide {_config.MaxSubgoalsPerGoal} or fewer subgoals that together accomplish the main goal.
For each subgoal, specify:
- Description (one clear sentence)
- Type (Primary/Secondary/Instrumental/Safety)
- Priority (0.0-1.0)

Format:
SUBGOAL 1: [description]
TYPE: [type]
PRIORITY: [0-1]

SUBGOAL 2: ...";

            string response = await _llm.GenerateTextAsync(prompt, ct);
            List<Goal> subgoals = ParseSubgoals(response, goal);

            // Recursively decompose subgoals if needed
            List<Goal> decomposedSubgoals = new List<Goal>();
            foreach (Goal subgoal in subgoals)
            {
                if (IsComplexGoal(subgoal) && maxDepth > 1)
                {
                    Result<Goal, string> decomposed = await DecomposeGoalAsync(subgoal, maxDepth - 1, ct);
                    decomposedSubgoals.Add(decomposed.IsSuccess ? decomposed.Value : subgoal);
                }
                else
                {
                    decomposedSubgoals.Add(subgoal);
                }
            }

            Goal updatedGoal = goal with { Subgoals = decomposedSubgoals };
            _goals[updatedGoal.Id] = updatedGoal;

            return Result<Goal, string>.Success(updatedGoal);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Result<Goal, string>.Failure($"Goal decomposition failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Detects conflicts between goals.
    /// </summary>
    public async Task<List<GoalConflict>> DetectConflictsAsync(CancellationToken ct = default)
    {
        List<GoalConflict> conflicts = new List<GoalConflict>();
        List<Goal> activeGoals = GetActiveGoals();

        // Check pairwise conflicts
        for (int i = 0; i < activeGoals.Count; i++)
        {
            for (int j = i + 1; j < activeGoals.Count; j++)
            {
                Goal goal1 = activeGoals[i];
                Goal goal2 = activeGoals[j];

                // Check for direct conflicts
                if (HasDirectConflict(goal1, goal2))
                {
                    conflicts.Add(new GoalConflict(
                        goal1,
                        goal2,
                        "Direct conflict",
                        $"Goals '{goal1.Description}' and '{goal2.Description}' conflict directly",
                        new List<string> { "Prioritize one goal", "Modify constraints", "Sequence goals" }));
                }

                // Check for resource conflicts
                if (HasResourceConflict(goal1, goal2))
                {
                    conflicts.Add(new GoalConflict(
                        goal1,
                        goal2,
                        "Resource conflict",
                        $"Goals compete for the same resources",
                        new List<string> { "Allocate resources", "Sequence goals", "Reduce scope" }));
                }

                // Use LLM for semantic conflict detection
                GoalConflict? semanticConflict = await DetectSemanticConflictAsync(goal1, goal2, ct);
                if (semanticConflict != null)
                {
                    conflicts.Add(semanticConflict);
                }
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Checks if a goal aligns with safety constraints and values.
    /// </summary>
    public async Task<Result<bool, string>> CheckValueAlignmentAsync(
        Goal goal,
        CancellationToken ct = default)
    {
        if (goal == null)
            return Result<bool, string>.Failure("Goal cannot be null");

        // Check safety constraints
        foreach (string constraint in _config.SafetyConstraints)
        {
            bool violates = await CheckConstraintViolationAsync(goal, constraint, ct);
            if (violates)
            {
                return Result<bool, string>.Failure(
                    $"Goal violates safety constraint: {constraint}");
            }
        }

        // Check value alignment using LLM
        string prompt = $@"Evaluate if this goal aligns with core values:

GOAL: {goal.Description}

CORE VALUES:
{string.Join("\n", _config.CoreValues.Select(v => $"- {v}"))}

SAFETY CONSTRAINTS:
{string.Join("\n", _config.SafetyConstraints.Select(c => $"- {c}"))}

Does this goal align with the values and respect the constraints?
Answer with 'ALIGNED' or 'MISALIGNED' followed by explanation.";

        string response = await _llm.GenerateTextAsync(prompt, ct);
        bool aligned = response.Contains("ALIGNED", StringComparison.OrdinalIgnoreCase) &&
                     !response.Contains("MISALIGNED", StringComparison.OrdinalIgnoreCase);

        if (!aligned)
        {
            return Result<bool, string>.Failure(
                $"Goal misaligned with values: {response}");
        }

        // Safety goals cannot be overridden
        if (goal.Type == GoalType.Safety)
        {
            // Ensure safety goals are always aligned
            return Result<bool, string>.Success(true);
        }

        return Result<bool, string>.Success(true);
    }

    /// <summary>
    /// Marks a goal as complete.
    /// </summary>
    public void CompleteGoal(Guid id, string reason)
    {
        if (_goals.TryGetValue(id, out Goal? goal))
        {
            Goal completed = goal with
            {
                IsComplete = true,
                CompletionReason = reason
            };
            _goals[id] = completed;
        }
    }

    /// <summary>
    /// Gets the goal hierarchy as a tree structure.
    /// </summary>
    public List<Goal> GetGoalTree()
    {
        // Return root goals (those without parents)
        return _goals.Values
            .Where(g => g.ParentGoal == null)
            .OrderByDescending(g => g.Priority)
            .ToList();
    }

    /// <summary>
    /// Prioritizes goals based on dependencies and importance.
    /// </summary>
    public async Task<List<Goal>> PrioritizeGoalsAsync(CancellationToken ct = default)
    {
        List<Goal> activeGoals = GetActiveGoals();

        // Use topological sort to respect dependencies
        List<Goal> prioritized = new List<Goal>();
        HashSet<Guid> visited = new HashSet<Guid>();

        // Helper function for dependency resolution
        void Visit(Goal goal)
        {
            if (visited.Contains(goal.Id))
                return;

            visited.Add(goal.Id);

            // Visit dependencies first (subgoals)
            foreach (Goal? subgoal in goal.Subgoals.Where(s => !s.IsComplete))
            {
                Visit(subgoal);
            }

            prioritized.Add(goal);
        }

        // Start with safety goals
        foreach (Goal? safetyGoal in activeGoals.Where(g => g.Type == GoalType.Safety))
        {
            Visit(safetyGoal);
        }

        // Then primary goals
        foreach (Goal? primaryGoal in activeGoals.Where(g => g.Type == GoalType.Primary))
        {
            Visit(primaryGoal);
        }

        // Then secondary and instrumental
        foreach (Goal? otherGoal in activeGoals.Where(g =>
            g.Type != GoalType.Safety && g.Type != GoalType.Primary))
        {
            Visit(otherGoal);
        }

        await Task.CompletedTask;
        return prioritized;
    }

}
