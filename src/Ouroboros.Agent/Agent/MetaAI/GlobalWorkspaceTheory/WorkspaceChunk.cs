// ==========================================================
// Global Workspace Theory — Capacity-Limited Workspace
// Plan 2: WorkspaceChunk record
// ==========================================================

namespace Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

/// <summary>
/// A chunk currently held in the capacity-limited global workspace.
/// </summary>
public sealed record WorkspaceChunk(Candidate Candidate, DateTime AdmittedAt, double Salience);
