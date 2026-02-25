namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a decomposition of an abstract task into concrete subtasks.
/// </summary>
public sealed record TaskDecomposition(
    string AbstractTask,
    List<string> SubTasks,
    List<string> OrderingConstraints);