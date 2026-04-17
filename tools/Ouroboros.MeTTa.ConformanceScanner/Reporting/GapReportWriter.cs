// <copyright file="GapReportWriter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using Ouroboros.MeTTa.ConformanceScanner.Inventory;
using Ouroboros.MeTTa.ConformanceScanner.Inventory;
using Ouroboros.MeTTa.ConformanceScanner.Spec;
using MismatchRow = Ouroboros.MeTTa.ConformanceScanner.Mismatch.Mismatch;
using MismatchKind = Ouroboros.MeTTa.ConformanceScanner.Mismatch.MismatchKind;

namespace Ouroboros.MeTTa.ConformanceScanner.Reporting;

/// <summary>
/// Writes a Markdown gap report for Phase 193/194 planning.
/// </summary>
public static class GapReportWriter
{
    /// <summary>
    /// Writes report to <paramref name="path"/> (UTF-8).
    /// </summary>
    public static async Task WriteAsync(
        string path,
        ParsedSpec spec,
        EngineInventory inventory,
        IReadOnlyList<MismatchRow> mismatches)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# MeTTa conformance gap report (Phase 193)");
        sb.AppendLine();
        sb.AppendLine($"**Generated (UTC):** {DateTimeOffset.UtcNow:O}");
        sb.AppendLine($"**Spec SHA256:** `{spec.SourceSha256}`");
        sb.AppendLine($"**Spec operations (grouped):** {spec.Operations.Count}");
        sb.AppendLine($"**Engine distinct names:** {inventory.AllNames.Count()}");
        sb.AppendLine();

        var missing = mismatches.Count(static m => m.Kind == MismatchKind.MissingInEngine);
        var denom = Math.Max(spec.Operations.Count, 1);
        var proxy = 1.0 - (missing / (double)denom);
        sb.AppendLine("## Baseline conformance proxy");
        sb.AppendLine();
        sb.AppendLine($"- **Approx coverage (1 - missing/specOps):** {proxy:P1}");
        sb.AppendLine("- **Note:** This is a coarse stdlib-vs-registration proxy (~80% gap called out in METTA-01 narrative is qualitative; Phase 194 tightens metrics).");
        sb.AppendLine();

        sb.AppendLine("## 1. Stdlib (spec-defined operations missing in engine)");
        sb.AppendLine();
        AppendSection(sb, mismatches, MismatchKind.MissingInEngine);
        sb.AppendLine();

        sb.AppendLine("## 2. Grounded operations (extras + signature deltas)");
        sb.AppendLine();
        sb.AppendLine("### Extra registrations (intentional extensions)");
        AppendSection(sb, mismatches, MismatchKind.ExtraInEngine);
        sb.AppendLine();
        sb.AppendLine("### Signature / arity inconsistencies (spec internal or vs registration)");
        AppendSection(sb, mismatches, MismatchKind.SignatureMismatch);
        sb.AppendLine();

        sb.AppendLine("## 3. Type-checking / grounding notes");
        sb.AppendLine();
        sb.AppendLine("See *Signature / arity inconsistencies* above for `SignatureMismatch` rows tied to `(: ...)` vs `(= ...)` forms.");
        sb.AppendLine();

        sb.AppendLine("## 4. Language-level semantic drift (curated)");
        sb.AppendLine();
        AppendSection(sb, mismatches, MismatchKind.SemanticDrift);
        sb.AppendLine();

        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8).ConfigureAwait(false);
    }

    private static void AppendSection(StringBuilder sb, IReadOnlyList<MismatchRow> mismatches, MismatchKind kind)
    {
        var rows = mismatches.Where(m => m.Kind == kind).ToList();
        if (rows.Count == 0)
        {
            sb.AppendLine("_None._");
            return;
        }

        foreach (var m in rows)
        {
            sb.AppendLine($"- **`{m.OpName}`** — _{kind}_");
            if (!string.IsNullOrEmpty(m.SpecSignature))
            {
                sb.AppendLine($"  - Spec type: `{m.SpecSignature}`");
            }

            if (!string.IsNullOrEmpty(m.EngineSource))
            {
                sb.AppendLine($"  - Engine source: `{m.EngineSource}`");
            }

            if (!string.IsNullOrWhiteSpace(m.Notes))
            {
                sb.AppendLine($"  - Notes: {m.Notes}");
            }
        }
    }
}
