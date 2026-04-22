// ==========================================================
// Global Workspace Theory — Competition-for-Broadcast
// Plan 1: ICandidateProducer interface
// ==========================================================

namespace Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

/// <summary>
/// Implemented by any subsystem that can produce workspace candidates.
/// </summary>
public interface ICandidateProducer
{
    /// <summary>
    /// Unique identifier for the producer subsystem.
    /// </summary>
    string SubsystemName { get; }

    /// <summary>
    /// Produces candidates for the current cognitive tick.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Collection of candidates competing for workspace access</returns>
    Task<IReadOnlyList<Candidate>> ProduceCandidatesAsync(CancellationToken ct = default);
}
