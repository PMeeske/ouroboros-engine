// <copyright file="GrammarAtomConverter.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

#pragma warning disable SA1309 // Field names should not begin with underscore
#pragma warning disable SA1101 // Prefix local calls with this

namespace Ouroboros.Pipeline.Grammar;

using Ouroboros.Core.Hyperon;

/// <summary>
/// Converts between .NET grammar types and MeTTa atom representations
/// for storage and querying in the local C# AtomSpace.
/// </summary>
/// <remarks>
/// This converter bridges the .NET grammar domain model with the symbolic
/// representation defined in <c>GrammarAtoms.metta</c>. It enables recording
/// grammar evolution events and querying grammar metadata through the local
/// Hyperon engine.
/// </remarks>
public static class GrammarAtomConverter
{
    /// <summary>
    /// Records a grammar state transition in the AtomSpace.
    /// </summary>
    /// <param name="engine">The Hyperon engine.</param>
    /// <param name="grammarName">The grammar name.</param>
    /// <param name="state">The new grammar state.</param>
    public static void RecordGrammarState(
        HyperonMeTTaEngine engine,
        string grammarName,
        string state)
    {
        ArgumentNullException.ThrowIfNull(engine);
        engine.AddAtom(Atom.Expr(
            Atom.Sym("GrammarInState"),
            Atom.Sym(grammarName),
            Atom.Sym(state)));
    }

    /// <summary>
    /// Records a grammar issue as an atom.
    /// </summary>
    /// <param name="engine">The Hyperon engine.</param>
    /// <param name="issue">The grammar issue.</param>
    public static void RecordIssue(HyperonMeTTaEngine engine, GrammarIssue issue)
    {
        ArgumentNullException.ThrowIfNull(engine);
        engine.AddAtom(Atom.Expr(
            Atom.Sym("IssueIn"),
            Atom.Sym(issue.RuleName),
            Atom.Sym(issue.Kind.ToString())));
    }

    /// <summary>
    /// Records a grammar evolution attempt.
    /// </summary>
    /// <param name="engine">The Hyperon engine.</param>
    /// <param name="grammarName">The grammar name.</param>
    /// <param name="attempt">Attempt number.</param>
    /// <param name="outcome">Outcome: "Success", "Failure", or "Corrected".</param>
    public static void RecordAttempt(
        HyperonMeTTaEngine engine,
        string grammarName,
        int attempt,
        string outcome)
    {
        ArgumentNullException.ThrowIfNull(engine);
        engine.AddAtom(Atom.Expr(
            Atom.Sym("MkAttempt"),
            Atom.Sym(grammarName),
            Atom.Sym(attempt.ToString()),
            Atom.Sym(DateTime.UtcNow.Ticks.ToString())));

        engine.AddAtom(Atom.Expr(
            Atom.Sym("HasOutcome"),
            Atom.Sym($"{grammarName}_{attempt}"),
            Atom.Sym($"Attempt{outcome}")));
    }

    /// <summary>
    /// Records the LLM generation source for a grammar.
    /// </summary>
    /// <param name="engine">The Hyperon engine.</param>
    /// <param name="grammarName">The grammar name.</param>
    /// <param name="modelName">The LLM model used.</param>
    /// <param name="temperature">The generation temperature.</param>
    public static void RecordGenerationSource(
        HyperonMeTTaEngine engine,
        string grammarName,
        string modelName,
        double temperature)
    {
        ArgumentNullException.ThrowIfNull(engine);
        engine.AddAtom(Atom.Expr(
            Atom.Sym("GeneratedBy"),
            Atom.Sym(grammarName),
            Atom.Expr(
                Atom.Sym("OllamaSource"),
                Atom.Sym(modelName))));

        engine.AddAtom(Atom.Expr(
            Atom.Sym("GenerationTemperature"),
            Atom.Sym(grammarName),
            Atom.Sym(temperature.ToString("F2"))));
    }

    /// <summary>
    /// Records a proven grammar in the AtomSpace.
    /// </summary>
    /// <param name="engine">The Hyperon engine.</param>
    /// <param name="grammarId">The grammar ID.</param>
    /// <param name="description">What the grammar parses.</param>
    /// <param name="grammarG4">The grammar content.</param>
    public static void RecordProvenGrammar(
        HyperonMeTTaEngine engine,
        string grammarId,
        string description,
        string grammarG4)
    {
        ArgumentNullException.ThrowIfNull(engine);
        engine.AddAtom(Atom.Expr(
            Atom.Sym("MkProvenGrammar"),
            Atom.Sym(grammarId),
            Atom.Sym(description),
            Atom.Sym(grammarG4)));

        RecordGrammarState(engine, grammarId, "Proven");
    }

    /// <summary>
    /// Converts a <see cref="GrammarIssueKind"/> to a MeTTa atom name.
    /// </summary>
    /// <param name="kind">The issue kind.</param>
    /// <returns>The MeTTa atom name.</returns>
    public static string ToMeTTaAtomName(GrammarIssueKind kind)
        => kind switch
        {
            GrammarIssueKind.LeftRecursion => "LeftRecursion",
            GrammarIssueKind.UnreachableRule => "UnreachableRule",
            GrammarIssueKind.FirstSetConflict => "FirstSetConflict",
            GrammarIssueKind.MissingRule => "MissingRule",
            GrammarIssueKind.SyntaxError => "SyntaxError",
            GrammarIssueKind.Ambiguity => "Ambiguity",
            _ => "Unspecified",
        };
}
