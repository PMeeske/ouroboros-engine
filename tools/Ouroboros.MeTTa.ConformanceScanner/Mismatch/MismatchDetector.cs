// <copyright file="MismatchDetector.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.MeTTa.ConformanceScanner.Inventory;
using Ouroboros.MeTTa.ConformanceScanner.Spec;

namespace Ouroboros.MeTTa.ConformanceScanner.Mismatch;

/// <summary>
/// Pure diff of <see cref="ParsedSpec"/> vs <see cref="EngineInventory"/>.
/// </summary>
public static class MismatchDetector
{
    /// <summary>
    /// Detects mismatches. Semantic drift catalogue entries are always included first.
    /// </summary>
    public static IReadOnlyList<Mismatch> Detect(ParsedSpec spec, EngineInventory inv)
    {
        var list = new List<Mismatch>();
        var engineByName = inv.AllOperations
            .Where(static op => !op.Name.StartsWith("<", StringComparison.Ordinal))
            .GroupBy(static op => op.Name, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.First(), StringComparer.Ordinal);

        string? EngineLookup(string name) =>
            engineByName.TryGetValue(name, out var op) ? op.RegistrationSource : null;

        list.AddRange(SemanticDriftCatalogue.AsMismatches(EngineLookup));

        var specNames = spec.Operations.Keys.ToHashSet(StringComparer.Ordinal);
        var engineNames = engineByName.Keys.ToHashSet(StringComparer.Ordinal);

        foreach (var name in specNames.Intersect(engineNames, StringComparer.Ordinal).OrderBy(static n => n, StringComparer.Ordinal))
        {
            if (SemanticDriftCatalogue.IsKnownDrift(name))
            {
                continue;
            }

            if (!spec.Operations.TryGetValue(name, out var schema))
            {
                continue;
            }

            var sigWithArity = schema.Signatures.FirstOrDefault(static s => s.Arity >= 0);
            var hasSig = sigWithArity is not null;
            var hasDef = schema.Definitions.Count > 0;
            var defArity = hasDef ? schema.Definitions[0].ArgArity : -1;

            if (hasSig && hasDef && sigWithArity!.Arity != defArity)
            {
                list.Add(new Mismatch(
                    name,
                    MismatchKind.SignatureMismatch,
                    sigWithArity.TypeExpression,
                    engineByName[name].RegistrationSource,
                    $"Spec type arity {sigWithArity.Arity} vs definition head arity {defArity}."));
            }
        }

        foreach (var name in specNames.Except(engineNames, StringComparer.Ordinal).OrderBy(static n => n, StringComparer.Ordinal))
        {
            var sigText = spec.Operations.TryGetValue(name, out var s)
                ? s.Signatures.FirstOrDefault()?.TypeExpression
                : null;
            list.Add(new Mismatch(name, MismatchKind.MissingInEngine, sigText, null, string.Empty));
        }

        foreach (var name in engineNames.Except(specNames, StringComparer.Ordinal).OrderBy(static n => n, StringComparer.Ordinal))
        {
            var op = engineByName[name];
            var extraNote = "Intentional Ouroboros extension — do not remove.";
            var alias = AliasMap.Lookup(name);
            if (alias is not null)
            {
                extraNote += " " + alias.Note;
                if (alias.SpecParallel is not null)
                {
                    extraNote += $" (parallel spec op: `{alias.SpecParallel}`)";
                }
            }

            list.Add(new Mismatch(name, MismatchKind.ExtraInEngine, null, op.RegistrationSource, extraNote));
        }

        static int KindOrder(MismatchKind k) => k switch
        {
            MismatchKind.SemanticDrift => 0,
            MismatchKind.MissingInEngine => 1,
            MismatchKind.SignatureMismatch => 2,
            MismatchKind.ExtraInEngine => 3,
            _ => 9,
        };

        return list
            .OrderBy(static m => KindOrder(m.Kind))
            .ThenBy(static m => m.OpName, StringComparer.Ordinal)
            .ToList();
    }
}
