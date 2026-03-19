// <copyright file="McpToolCallAtomConverter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
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
/// </remarks>
public static class McpToolCallAtomConverter
{
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

    [System.Text.RegularExpressions.GeneratedRegex(
        @"\(MkToolCall\s+""([^""\\]*(?:\\.[^""\\]*)*)""\s+""([^""\\]*(?:\\.[^""\\]*)*)""\)",
        System.Text.RegularExpressions.RegexOptions.Compiled)]
    private static partial System.Text.RegularExpressions.Regex MkToolCallRegex();
}
