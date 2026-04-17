// <copyright file="PatchEmitter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using MismatchRow = Ouroboros.MeTTa.ConformanceScanner.Mismatch.Mismatch;
using MismatchKind = Ouroboros.MeTTa.ConformanceScanner.Mismatch.MismatchKind;

namespace Ouroboros.MeTTa.ConformanceScanner.Patches;

/// <summary>
/// Maps mismatch rows to reviewable patch proposals (no code edits).
/// </summary>
public static class PatchEmitter
{
    /// <summary>
    /// Emits one proposal per mismatch with risk heuristics (semantic drift highest).
    /// </summary>
    public static IReadOnlyList<PatchProposal> Emit(IReadOnlyList<MismatchRow> mismatches)
    {
        var list = new List<PatchProposal>(mismatches.Count);
        var i = 0;
        foreach (var m in mismatches)
        {
            i++;
            var id = $"{m.OpName}-{m.Kind}-{i:000}";
            var (category, proposed, risk) = Map(m);
            list.Add(new PatchProposal(
                id,
                category,
                m.SpecSignature,
                m.EngineSource,
                proposed,
                risk,
                m.Notes));
        }

        return list;
    }

    private static (string Category, string ProposedChange, string RiskLevel) Map(MismatchRow m) =>
        m.Kind switch
        {
            MismatchKind.MissingInEngine => (
                "stdlib-grounded",
                $"Add grounded implementation for `{m.OpName}` aligned with stdlib snapshot (see spec signature).",
                "MEDIUM"),
            MismatchKind.ExtraInEngine => (
                "extension",
                $"Keep `{m.OpName}` as Ouroboros extension; document in operator catalogue / gap report only.",
                "LOW"),
            MismatchKind.SignatureMismatch => (
                "type-grounding",
                $"Reconcile arity or type between stdlib `(: ...)` and `(= ({m.OpName} ...) ...)` vs engine registration.",
                "MEDIUM"),
            MismatchKind.SemanticDrift => (
                "language-semantics",
                $"Formalize divergence for `{m.OpName}` (spec vs Ouroboros) — align implementation or publish compatibility contract.",
                "HIGH"),
            _ => ("unknown", "Review manually.", "MEDIUM"),
        };
}
