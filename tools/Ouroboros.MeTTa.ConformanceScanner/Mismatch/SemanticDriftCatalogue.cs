// <copyright file="SemanticDriftCatalogue.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.ConformanceScanner.Mismatch;

/// <summary>
/// Hand-curated semantic divergences between stdlib spec and Iaret's engine.
/// </summary>
public static class SemanticDriftCatalogue
{
    public static readonly IReadOnlyList<(string Name, string DivergenceNote)> KnownDrifts =
        new (string, string)[]
        {
            ("bind!",
                "Ouroboros stores (bind! key value) as a (bound key value) fact + removes prior binding. " +
                "Spec defines bind! as a dynamic-symbol binding primitive for module-level mutable state. " +
                "Same name, different semantics. Phase 194 planner decides whether to align or document divergence."),
            ("match",
                "Ouroboros `match` takes 1 argument (the pattern); spec requires 3 arguments " +
                "(match space pattern template). Arity and semantics both diverge."),
            ("cons",
                "Ouroboros `cons` is a 2-argument concatenation via Atom.Expr (not head+tail). " +
                "Spec has no `cons`; spec's head+tail constructor is `cons-atom`. The two co-exist."),
            ("import!",
                "Ouroboros `import!` records (imported path) as a fact only — does not load module content. " +
                "Spec `import!` loads module content into the atomspace."),
        };

    public static bool IsKnownDrift(string opName) =>
        KnownDrifts.Any(d => d.Name.Equals(opName, StringComparison.Ordinal));

    public static IEnumerable<Mismatch> AsMismatches(Func<string, string?> engineSourceLookup) =>
        KnownDrifts.Select(d => new Mismatch(
            OpName: d.Name,
            Kind: MismatchKind.SemanticDrift,
            SpecSignature: null,
            EngineSource: engineSourceLookup(d.Name),
            Notes: d.DivergenceNote));
}
