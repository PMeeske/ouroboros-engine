namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a hypothesis about system behavior or domain knowledge.
/// </summary>
public sealed record Hypothesis(
    Guid Id,
    string Statement,
    string Domain,
    double Confidence,
    List<string> SupportingEvidence,
    List<string> CounterEvidence,
    DateTime CreatedAt,
    bool Tested,
    bool? Validated);