// <copyright file="McpToolCallAtomConverter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Ouroboros.Core.Hyperon;
using Ouroboros.Providers;

namespace Ouroboros.Pipeline;

/// <summary>
/// Converts between .NET <see cref="ToolCallIntent"/> objects and MeTTa atom representations
/// defined in <c>ToolCallAtoms.metta</c>.
/// </summary>
/// <remarks>
/// Analogous to <see cref="Grammar.GrammarAtomConverter"/> for the grammar domain.
/// Enables neuro-symbolic reasoning over tool call intents, permission checks,
/// chain discovery, and evolutionary retry tracking.
/// <para>
/// Supports both string generation (for logging/display) and direct <see cref="HyperonMeTTaEngine"/>
/// recording (for AtomSpace queries and backward chaining). The <c>Record*</c> methods
/// write atoms directly to the engine, following the same pattern as
/// <see cref="Grammar.GrammarAtomConverter.RecordGrammarState"/>.
/// </para>
/// </remarks>
public static partial class McpToolCallAtomConverter
{
    // ── String generation (for display, logging, MeTTa text files) ─────────────

    /// <summary>
    /// Converts a single tool call intent to its MeTTa atom representation.
    /// </summary>
    /// <param name="intent">The tool call intent.</param>
    /// <returns>A MeTTa expression string, e.g. <c>(MkToolCall "search" "{\"query\":\"test\"}")</c>.</returns>
    public static string ToAtom(ToolCallIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        string escapedArgs = EscapeMeTTaString(intent.ArgumentsJson);
        return $"(MkToolCall \"{EscapeMeTTaString(intent.ToolName)}\" \"{escapedArgs}\")";
    }

    /// <summary>
    /// Converts a list of tool call intents to MeTTa atoms.
    /// Multiple intents are chained with <c>MkToolChain</c>.
    /// </summary>
    /// <param name="intents">The tool call intents.</param>
    /// <returns>MeTTa expression(s) — one per line for multiple uncoupled intents,
    /// or a single <c>MkToolChain</c> for sequential chains.</returns>
    public static string ToAtoms(IReadOnlyList<ToolCallIntent> intents)
    {
        ArgumentNullException.ThrowIfNull(intents);

        if (intents.Count == 0)
        {
            return string.Empty;
        }

        if (intents.Count == 1)
        {
            return ToAtom(intents[0]);
        }

        var sb = new StringBuilder();
        foreach (ToolCallIntent intent in intents)
        {
            sb.AppendLine(ToAtom(intent));
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Converts tool call intents into a chained MeTTa expression.
    /// </summary>
    /// <param name="intents">The ordered tool call intents to chain.</param>
    /// <returns>A nested <c>(MkToolChain ...)</c> expression.</returns>
    public static string ToChainAtom(IReadOnlyList<ToolCallIntent> intents)
    {
        ArgumentNullException.ThrowIfNull(intents);

        if (intents.Count == 0)
        {
            return string.Empty;
        }

        if (intents.Count == 1)
        {
            return ToAtom(intents[0]);
        }

        // Build right-associative chain: (MkToolChain a (MkToolChain b c))
        string result = ToAtom(intents[^1]);
        for (int i = intents.Count - 2; i >= 0; i--)
        {
            result = $"(MkToolChain {ToAtom(intents[i])} {result})";
        }

        return result;
    }

    /// <summary>
    /// Generates a MeTTa permission check expression for a tool call intent.
    /// </summary>
    /// <param name="intent">The tool call intent.</param>
    /// <param name="safetyContext">The safety context ("ReadOnly" or "FullAccess").</param>
    /// <returns>A MeTTa expression like <c>(ToolCallAllowed (MkToolCall "x" "{}") ReadOnly)</c>.</returns>
    public static string ToPermissionCheck(ToolCallIntent intent, string safetyContext)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(safetyContext);
        return $"(ToolCallAllowed {ToAtom(intent)} {safetyContext})";
    }

    /// <summary>
    /// Generates a MeTTa format tracking atom.
    /// </summary>
    /// <param name="intent">The tool call intent.</param>
    /// <returns>A <c>(ParsedFrom ...)</c> expression.</returns>
    public static string ToFormatAtom(ToolCallIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        string formatName = intent.Format switch
        {
            ToolCallFormat.XmlTag => "XmlTagFormat",
            ToolCallFormat.JsonToolCalls => "JsonToolCallsFormat",
            ToolCallFormat.BracketLegacy => "BracketLegacyFormat",
            ToolCallFormat.NativeApi => "NativeApiFormat",
            ToolCallFormat.MarkdownBlock => "XmlTagFormat", // treated as XML variant
            _ => "XmlTagFormat"
        };
        return $"(ParsedFrom {ToAtom(intent)} {formatName})";
    }

    /// <summary>
    /// Generates a MeTTa tool call result atom.
    /// </summary>
    /// <param name="toolName">The tool name.</param>
    /// <param name="output">The tool execution output.</param>
    /// <param name="isError">Whether the result is an error.</param>
    /// <returns>A <c>(MkToolResult ...)</c> expression.</returns>
    public static string ToResultAtom(string toolName, string output, bool isError)
    {
        return $"(MkToolResult \"{EscapeMeTTaString(toolName)}\" \"{EscapeMeTTaString(output)}\" {(isError ? "True" : "False")})";
    }

    /// <summary>
    /// Generates MeTTa atoms tracking an evolutionary retry mutation.
    /// </summary>
    /// <param name="attemptId">Unique identifier for the retry attempt.</param>
    /// <param name="strategyName">Name of the mutation strategy used.</param>
    /// <param name="generation">The generation/iteration number.</param>
    /// <param name="outcome">The outcome of the mutation ("MutationSuccess", "MutationFailure", or "MutationEvolved").</param>
    /// <returns>MeTTa atoms recording the mutation and its outcome.</returns>
    public static string ToRetryMutationAtom(string attemptId, string strategyName, int generation, string outcome)
    {
        var sb = new StringBuilder();
        string mutationAtom = $"(MkRetryMutation \"{EscapeMeTTaString(attemptId)}\" \"{EscapeMeTTaString(strategyName)}\" {generation.ToString(CultureInfo.InvariantCulture)})";
        sb.AppendLine(mutationAtom);
        sb.Append($"(HasMutationOutcome {mutationAtom} {outcome})");
        return sb.ToString();
    }

    /// <summary>
    /// Parses a MeTTa <c>MkToolCall</c> atom back into a <see cref="ToolCallIntent"/>.
    /// </summary>
    /// <param name="mettaAtom">The MeTTa atom string.</param>
    /// <returns>The parsed intent, or <c>null</c> if the atom is not a valid MkToolCall.</returns>
    public static ToolCallIntent? FromAtom(string mettaAtom)
    {
        if (string.IsNullOrWhiteSpace(mettaAtom))
        {
            return null;
        }

        // Match: (MkToolCall "name" "args")
        var match = MkToolCallRegex().Match(mettaAtom);
        if (!match.Success)
        {
            return null;
        }

        string name = UnescapeMeTTaString(match.Groups[1].Value);
        string args = UnescapeMeTTaString(match.Groups[2].Value);
        return new ToolCallIntent(name, args, ToolCallFormat.NativeApi);
    }

    // ── HyperonMeTTaEngine recording (direct AtomSpace writes) ─────────────────

    /// <summary>
    /// Records a tool call intent as atoms in the HyperonMeTTaEngine AtomSpace.
    /// Follows the pattern of <see cref="Grammar.GrammarAtomConverter.RecordGrammarState"/>.
    /// </summary>
    /// <param name="engine">The Hyperon engine to write atoms to.</param>
    /// <param name="intent">The tool call intent.</param>
    public static void RecordToolCall(Ouroboros.Tools.MeTTa.HyperonMeTTaEngine engine, ToolCallIntent intent)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(intent);

        // Record the tool call intent
        engine.AddAtom(Atom.Expr(
            Atom.Sym("MkToolCall"),
            Atom.Sym(intent.ToolName),
            Atom.Sym(intent.ArgumentsJson)));

        // Record the format used
        string formatName = intent.Format switch
        {
            ToolCallFormat.XmlTag => "XmlTagFormat",
            ToolCallFormat.JsonToolCalls => "JsonToolCallsFormat",
            ToolCallFormat.BracketLegacy => "BracketLegacyFormat",
            ToolCallFormat.NativeApi => "NativeApiFormat",
            ToolCallFormat.MarkdownBlock => "MarkdownBlockFormat",
            _ => "UnknownFormat"
        };

        engine.AddAtom(Atom.Expr(
            Atom.Sym("ParsedFrom"),
            Atom.Expr(
                Atom.Sym("MkToolCall"),
                Atom.Sym(intent.ToolName),
                Atom.Sym(intent.ArgumentsJson)),
            Atom.Sym(formatName)));
    }

    /// <summary>
    /// Records a tool call chain in the AtomSpace.
    /// </summary>
    /// <param name="engine">The Hyperon engine.</param>
    /// <param name="intents">The ordered tool call intents forming the chain.</param>
    public static void RecordToolChain(Ouroboros.Tools.MeTTa.HyperonMeTTaEngine engine, IReadOnlyList<ToolCallIntent> intents)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(intents);

        if (intents.Count < 2) return;

        // Record each pair as a chain step
        for (int i = 0; i < intents.Count - 1; i++)
        {
            engine.AddAtom(Atom.Expr(
                Atom.Sym("MkToolChain"),
                Atom.Expr(
                    Atom.Sym("MkToolCall"),
                    Atom.Sym(intents[i].ToolName),
                    Atom.Sym(intents[i].ArgumentsJson)),
                Atom.Expr(
                    Atom.Sym("MkToolCall"),
                    Atom.Sym(intents[i + 1].ToolName),
                    Atom.Sym(intents[i + 1].ArgumentsJson))));
        }
    }

    /// <summary>
    /// Records a tool call result in the AtomSpace.
    /// </summary>
    /// <param name="engine">The Hyperon engine.</param>
    /// <param name="toolName">The tool name.</param>
    /// <param name="output">The execution output.</param>
    /// <param name="isError">Whether the result was an error.</param>
    public static void RecordToolResult(Ouroboros.Tools.MeTTa.HyperonMeTTaEngine engine, string toolName, string output, bool isError)
    {
        ArgumentNullException.ThrowIfNull(engine);

        engine.AddAtom(Atom.Expr(
            Atom.Sym("MkToolResult"),
            Atom.Sym(toolName),
            Atom.Sym(output),
            Atom.Sym(isError ? "True" : "False")));
    }

    /// <summary>
    /// Records a permission check in the AtomSpace.
    /// </summary>
    /// <param name="engine">The Hyperon engine.</param>
    /// <param name="intent">The tool call intent.</param>
    /// <param name="safetyContext">The safety context.</param>
    /// <param name="allowed">Whether the call was allowed.</param>
    public static void RecordPermissionCheck(
        Ouroboros.Tools.MeTTa.HyperonMeTTaEngine engine, ToolCallIntent intent, string safetyContext, bool allowed)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(intent);

        engine.AddAtom(Atom.Expr(
            Atom.Sym("ToolCallAllowed"),
            Atom.Expr(
                Atom.Sym("MkToolCall"),
                Atom.Sym(intent.ToolName),
                Atom.Sym(intent.ArgumentsJson)),
            Atom.Sym(safetyContext),
            Atom.Sym(allowed ? "True" : "False")));
    }

    /// <summary>
    /// Records an evolutionary retry mutation event in the AtomSpace.
    /// Follows the pattern of <see cref="Grammar.GrammarAtomConverter.RecordAttempt"/>.
    /// </summary>
    /// <param name="engine">The Hyperon engine.</param>
    /// <param name="attemptId">Unique attempt identifier.</param>
    /// <param name="strategyName">The mutation strategy used.</param>
    /// <param name="generation">The generation number.</param>
    /// <param name="outcome">The outcome: "MutationSuccess", "MutationFailure", or "MutationEvolved".</param>
    public static void RecordRetryMutation(
        Ouroboros.Tools.MeTTa.HyperonMeTTaEngine engine, string attemptId, string strategyName, int generation, string outcome)
    {
        ArgumentNullException.ThrowIfNull(engine);

        // Record the mutation
        engine.AddAtom(Atom.Expr(
            Atom.Sym("MkRetryMutation"),
            Atom.Sym(attemptId),
            Atom.Sym(strategyName),
            Atom.Sym(generation.ToString(CultureInfo.InvariantCulture))));

        // Record the outcome
        engine.AddAtom(Atom.Expr(
            Atom.Sym("HasMutationOutcome"),
            Atom.Expr(
                Atom.Sym("MkRetryMutation"),
                Atom.Sym(attemptId),
                Atom.Sym(strategyName),
                Atom.Sym(generation.ToString(CultureInfo.InvariantCulture))),
            Atom.Sym(outcome)));
    }

    /// <summary>
    /// Records chromosome fitness after evolutionary retry execution.
    /// </summary>
    /// <param name="engine">The Hyperon engine.</param>
    /// <param name="attemptId">Unique attempt identifier.</param>
    /// <param name="fitness">The fitness score.</param>
    /// <param name="succeeded">Whether the execution succeeded.</param>
    /// <param name="generationsUsed">How many generations were used.</param>
    public static void RecordFitnessEvaluation(
        Ouroboros.Tools.MeTTa.HyperonMeTTaEngine engine, string attemptId, double fitness, bool succeeded, int generationsUsed)
    {
        ArgumentNullException.ThrowIfNull(engine);

        engine.AddAtom(Atom.Expr(
            Atom.Sym("FitnessResult"),
            Atom.Sym(attemptId),
            Atom.Sym(fitness.ToString("F4", CultureInfo.InvariantCulture)),
            Atom.Sym(succeeded ? "Succeeded" : "Failed"),
            Atom.Sym(generationsUsed.ToString(CultureInfo.InvariantCulture))));
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static string EscapeMeTTaString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal);
    }

    private static string UnescapeMeTTaString(string value)
    {
        return value
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    // ── SMCP integration (records SMCP activity alongside MCP) ──────────────

    /// <summary>
    /// Records an SMCP intent atom in the engine's AtomSpace for audit/provenance.
    /// <c>(SmcpIntentRecord verb args confidence timestamp)</c>
    /// </summary>
    public static void RecordSmcpIntent(Ouroboros.Tools.MeTTa.HyperonMeTTaEngine engine,
        string verb, string argsDescription, double confidence)
    {
        engine.AddAtom(Atom.Expr(
            Atom.Sym("SmcpIntentRecord"),
            Atom.Sym(verb),
            Atom.Sym($"\"{EscapeMeTTaString(argsDescription)}\""),
            Atom.Sym(confidence.ToString("F4", CultureInfo.InvariantCulture)),
            Atom.Sym(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture))));
    }

    /// <summary>
    /// Records an SMCP tool activation event in the engine's AtomSpace.
    /// <c>(SmcpActivationRecord toolName compositeConfidence gateDecision timestamp)</c>
    /// </summary>
    public static void RecordSmcpActivation(Ouroboros.Tools.MeTTa.HyperonMeTTaEngine engine,
        string toolName, double compositeConfidence, string gateDecision)
    {
        engine.AddAtom(Atom.Expr(
            Atom.Sym("SmcpActivationRecord"),
            Atom.Sym($"\"{EscapeMeTTaString(toolName)}\""),
            Atom.Sym(compositeConfidence.ToString("F4", CultureInfo.InvariantCulture)),
            Atom.Sym(gateDecision),
            Atom.Sym(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture))));
    }

    [System.Text.RegularExpressions.GeneratedRegex(
        @"\(MkToolCall\s+""([^""\\]*(?:\\.[^""\\]*)*)""\s+""([^""\\]*(?:\\.[^""\\]*)*)""\)",
        System.Text.RegularExpressions.RegexOptions.Compiled)]
    private static partial System.Text.RegularExpressions.Regex MkToolCallRegex();
}
