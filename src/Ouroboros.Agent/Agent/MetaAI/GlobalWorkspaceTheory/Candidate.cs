// ==========================================================
// Global Workspace Theory — Competition-for-Broadcast
// Plan 1: Candidate record
// ==========================================================

namespace Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

/// <summary>
/// A candidate competing for entry into the global workspace.
/// </summary>
public sealed record Candidate(
    Guid Id,
    object Content,
    double Urgency,
    double Novelty,
    double Relevance,
    double Confidence,
    string SourceSubsystem)
{
    /// <summary>
    /// Creates a candidate with a new GUID.
    /// </summary>
    public Candidate(
        object Content,
        double Urgency,
        double Novelty,
        double Relevance,
        double Confidence,
        string SourceSubsystem)
        : this(
            Guid.NewGuid(),
            Content,
            Urgency,
            Novelty,
            Relevance,
            Confidence,
            SourceSubsystem)
    {
    }
}
