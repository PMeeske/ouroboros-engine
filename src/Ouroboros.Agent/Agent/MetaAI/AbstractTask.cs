namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents an abstract task that can be decomposed into subtasks.
/// </summary>
public sealed record AbstractTask(
    string Name,
    List<string> Preconditions,
    List<TaskDecomposition> PossibleDecompositions);