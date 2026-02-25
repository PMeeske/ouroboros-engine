namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a candidate next node in the execution graph.
/// </summary>
public sealed record NextNodeCandidate(
    string NodeId,
    string Action,
    double Confidence
);