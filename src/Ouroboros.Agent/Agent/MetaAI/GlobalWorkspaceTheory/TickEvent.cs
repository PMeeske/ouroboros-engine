// ==========================================================
// Global Workspace Theory — Cognitive Tick Loop
// Plan 5: TickEvent record for replay
// ==========================================================

namespace Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

/// <summary>
/// A recorded cognitive tick for replay and persistence.
/// </summary>
public sealed record TickEvent(
    long TickNumber,
    DateTime Timestamp,
    double DurationMs,
    IReadOnlyList<TickChunkRecord> WorkspaceChunks,
    int BroadcastReceiverCount,
    double Entropy)
{
    /// <summary>
    /// Creates a tick event from a workspace snapshot and report data.
    /// </summary>
    public static TickEvent FromReportAndSnapshot(
        long tickNumber,
        ConsciousAccessReport report,
        WorkspaceSnapshot snapshot)
    {
        return new TickEvent(
            tickNumber,
            report.Timestamp,
            report.DurationMs,
            snapshot.Chunks.Select(c => new TickChunkRecord(
                c.Candidate.Id,
                c.Candidate.Content?.ToString() ?? "",
                c.Candidate.SourceSubsystem,
                c.AdmittedAt)).ToList(),
            report.BroadcastReceiverCount,
            report.Entropy);
    }
}

/// <summary>
/// Minimal record of a chunk within a tick event.
/// </summary>
public sealed record TickChunkRecord(
    Guid CandidateId,
    string Content,
    string SourceSubsystem,
    DateTime AdmittedAt);
