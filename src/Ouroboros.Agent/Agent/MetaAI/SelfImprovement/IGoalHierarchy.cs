#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Goal Hierarchy Interface
// Hierarchical goal decomposition and value alignment
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Interface for hierarchical goal management and value alignment.
/// </summary>
public interface IGoalHierarchy
{
    /// <summary>
    /// Adds a goal to the hierarchy.
    /// </summary>
    /// <param name="goal">The goal to add</param>
    void AddGoal(Goal goal);

    /// <summary>
    /// Gets a goal by ID.
    /// </summary>
    /// <param name="id">The goal ID</param>
    /// <returns>The goal if found, null otherwise</returns>
    Goal? GetGoal(Guid id);

    /// <summary>
    /// Gets all active goals (not completed).
    /// </summary>
    /// <returns>List of active goals</returns>
    List<Goal> GetActiveGoals();

    /// <summary>
    /// Decomposes a complex goal into subgoals.
    /// </summary>
    /// <param name="goal">The goal to decompose</param>
    /// <param name="maxDepth">Maximum decomposition depth</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The goal with populated subgoals</returns>
    Task<Result<Goal, string>> DecomposeGoalAsync(
        Goal goal,
        int maxDepth = 3,
        CancellationToken ct = default);

    /// <summary>
    /// Detects conflicts between goals.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of detected conflicts</returns>
    Task<List<GoalConflict>> DetectConflictsAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks if a goal aligns with safety constraints and values.
    /// </summary>
    /// <param name="goal">The goal to check</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if aligned, false otherwise with reason</returns>
    Task<Result<bool, string>> CheckValueAlignmentAsync(
        Goal goal,
        CancellationToken ct = default);

    /// <summary>
    /// Marks a goal as complete.
    /// </summary>
    /// <param name="id">The goal ID</param>
    /// <param name="reason">Completion reason</param>
    void CompleteGoal(Guid id, string reason);

    /// <summary>
    /// Gets the goal hierarchy as a tree structure.
    /// </summary>
    /// <returns>Root goals with their subgoal trees</returns>
    List<Goal> GetGoalTree();

    /// <summary>
    /// Prioritizes goals based on dependencies and importance.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Ordered list of goals to pursue</returns>
    Task<List<Goal>> PrioritizeGoalsAsync(CancellationToken ct = default);
}
