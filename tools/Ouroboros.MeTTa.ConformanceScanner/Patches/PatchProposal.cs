// <copyright file="PatchProposal.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.ConformanceScanner.Patches;

/// <summary>
/// JSON-serializable conformance patch candidate (Phase 194 consumes; not applied here).
/// </summary>
public sealed record PatchProposal(
    string Id,
    string Category,
    string? SpecSignature,
    string? CurrentImplRef,
    string ProposedChange,
    string RiskLevel,
    string Notes);
