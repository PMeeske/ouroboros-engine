// <copyright file="AliasMap.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.ConformanceScanner.Mismatch;

/// <summary>
/// Ouroboros extensions that intentionally co-exist with or parallel spec operations.
/// </summary>
public static class AliasMap
{
    public static readonly IReadOnlyList<AliasEntry> Extensions = new[]
    {
        new AliasEntry("cons", "cons-atom", "Ouroboros 2-arg Atom.Expr concatenation; spec equivalent is `cons-atom` (head+tail)."),
        new AliasEntry("car", "car-atom", "Ouroboros extension; spec equivalent is `car-atom`."),
        new AliasEntry("cdr", "cdr-atom", "Ouroboros extension; spec equivalent is `cdr-atom`."),
        new AliasEntry("+", null, "Ouroboros tools-engine arithmetic; not in pinned stdlib surface."),
        new AliasEntry("-", null, "Ouroboros tools-engine arithmetic; not in pinned stdlib surface."),
        new AliasEntry("*", null, "Ouroboros tools-engine arithmetic; not in pinned stdlib surface."),
        new AliasEntry("/", null, "Ouroboros tools-engine arithmetic; not in pinned stdlib surface."),
        new AliasEntry("==", null, "Ouroboros tools-engine comparison; not in pinned stdlib surface."),
        new AliasEntry("!=", null, "Ouroboros tools-engine comparison; not in pinned stdlib surface."),
        new AliasEntry("println", null, "Ouroboros I/O helper; not in pinned stdlib surface."),
        new AliasEntry("and-all", null, "Ouroboros reduction operator — aggregate truth across atoms; no spec parallel."),
        new AliasEntry("or-any", null, "Ouroboros reduction operator — disjunctive truth across atoms; no spec parallel."),
        new AliasEntry("negate", null, "Ouroboros boolean negation; spec uses `not`."),
        new AliasEntry("concat-str", null, "Ouroboros string concatenation primitive; not in spec."),
        new AliasEntry("identity", null, "Ouroboros identity function for grounded-op pass-through; not in spec."),
        new AliasEntry("reflect", null, "Ouroboros self-reflection op."),
        new AliasEntry("introspect", null, "Ouroboros introspection op."),
        new AliasEntry("check-steps", null, "Ouroboros planner trace op."),
        new AliasEntry("think", null, "Ouroboros cognitive-invoke op."),
        new AliasEntry("remember", null, "Ouroboros memory-store op."),
        new AliasEntry("intend", null, "Ouroboros intention-emit op."),
        new AliasEntry("llm-infer", null, "Ouroboros neural bridge — late-bound by BindNeuralModels."),
        new AliasEntry("llm-code", null, "Ouroboros neural bridge — late-bound by BindNeuralModels."),
        new AliasEntry("llm-reason", null, "Ouroboros neural bridge — late-bound by BindNeuralModels."),
        new AliasEntry("llm-summarize", null, "Ouroboros neural bridge — late-bound by BindNeuralModels."),
        new AliasEntry("llm-tools", null, "Ouroboros neural bridge — late-bound by BindNeuralModels."),
        new AliasEntry("llm-route", null, "Ouroboros neural bridge — late-bound by BindNeuralModels."),
        new AliasEntry("llm-available", null, "Ouroboros neural bridge — late-bound by BindNeuralModels."),
    };

    public static AliasEntry? Lookup(string extensionName) =>
        Extensions.FirstOrDefault(e => e.ExtensionName.Equals(extensionName, StringComparison.Ordinal));

    public static bool IsKnownExtension(string name) => Lookup(name) is not null;
}

/// <summary>
/// One extension entry for gap-report annotation.
/// </summary>
public sealed record AliasEntry(string ExtensionName, string? SpecParallel, string Note);
