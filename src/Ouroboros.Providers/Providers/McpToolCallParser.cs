// <copyright file="McpToolCallParser.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ouroboros.Providers;

/// <summary>
/// Parses tool call intents from unstructured LLM output in multiple formats.
/// Implements the grammar specified in <c>ToolCallDsl.g4</c> as a hand-written
/// parser for zero build-tool overhead.
/// </summary>
/// <remarks>
/// Recognized formats (in detection priority order):
/// <list type="number">
/// <item><c>&lt;tool_call&gt;{"name":"x","arguments":{...}}&lt;/tool_call&gt;</c> — Mistral/Qwen/Hermes</item>
/// <item><c>{"tool_calls":[{"function":{"name":"x","arguments":{...}}}]}</c> — OpenAI-compatible JSON</item>
/// <item><c>```tool_call\n{"name":"x",...}\n```</c> — Markdown code block</item>
/// <item><c>[TOOL:name args]</c> — Ouroboros legacy bracket format</item>
/// </list>
/// </remarks>
public sealed partial class McpToolCallParser
{
    /// <summary>
    /// Parses all tool call intents from the given LLM output text.
    /// </summary>
    /// <param name="llmOutput">Raw text output from an LLM that may contain tool calls.</param>
    /// <returns>A list of parsed tool call intents. Empty if no tool calls found.</returns>
    public IReadOnlyList<ToolCallIntent> Parse(string llmOutput)
    {
        if (string.IsNullOrWhiteSpace(llmOutput))
        {
            return [];
        }

        var results = new List<ToolCallIntent>();

        // Priority 1: XML tag format — <tool_call>{...}</tool_call>
        results.AddRange(ParseXmlToolCalls(llmOutput));

        // Priority 2: JSON tool_calls array — {"tool_calls":[...]}
        results.AddRange(ParseJsonToolCalls(llmOutput));

        // Priority 3: Markdown code block — ```tool_call\n{...}\n```
        results.AddRange(ParseMarkdownToolCalls(llmOutput));

        // Priority 4: Bracket legacy — [TOOL:name args]
        results.AddRange(ParseBracketToolCalls(llmOutput));

        return results;
    }

    /// <summary>
    /// Extracts the non-tool-call text segments from the LLM output.
    /// Useful for preserving the conversational response alongside tool executions.
    /// </summary>
    /// <param name="llmOutput">Raw text output from an LLM.</param>
    /// <returns>The text with all tool call patterns removed.</returns>
    public string ExtractTextSegments(string llmOutput)
    {
        if (string.IsNullOrWhiteSpace(llmOutput))
        {
            return string.Empty;
        }

        string result = llmOutput;
        result = XmlToolCallRegex().Replace(result, "");
        result = JsonToolCallsRegex().Replace(result, "");
        result = MarkdownToolCallRegex().Replace(result, "");
        result = BracketToolCallRegex().Replace(result, "");
        return result.Trim();
    }

    // ── XML format: <tool_call>{...}</tool_call> ─────────────────────────────

    private static List<ToolCallIntent> ParseXmlToolCalls(string text)
    {
        var results = new List<ToolCallIntent>();
        foreach (Match match in XmlToolCallRegex().Matches(text))
        {
            string json = match.Groups[1].Value.Trim();
            var intent = ParseToolCallJson(json, ToolCallFormat.XmlTag);
            if (intent is not null)
            {
                results.Add(intent);
            }
        }

        return results;
    }

    // ── JSON format: {"tool_calls":[...]} ────────────────────────────────────

    private static List<ToolCallIntent> ParseJsonToolCalls(string text)
    {
        var results = new List<ToolCallIntent>();
        foreach (Match match in JsonToolCallsRegex().Matches(text))
        {
            string json = match.Value.Trim();
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("tool_calls", out JsonElement toolCalls)
                    || toolCalls.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (JsonElement item in toolCalls.EnumerateArray())
                {
                    var intent = ParseToolCallElement(item, ToolCallFormat.JsonToolCalls);
                    if (intent is not null)
                    {
                        results.Add(intent);
                    }
                }
            }
            catch (JsonException)
            {
                // Malformed JSON — skip
            }
        }

        return results;
    }

    // ── Markdown format: ```tool_call\n{...}\n``` ────────────────────────────

    private static List<ToolCallIntent> ParseMarkdownToolCalls(string text)
    {
        var results = new List<ToolCallIntent>();
        foreach (Match match in MarkdownToolCallRegex().Matches(text))
        {
            string json = match.Groups[1].Value.Trim();
            var intent = ParseToolCallJson(json, ToolCallFormat.XmlTag); // same JSON structure
            if (intent is not null)
            {
                results.Add(intent with { Format = ToolCallFormat.MarkdownBlock });
            }
        }

        return results;
    }

    // ── Bracket format: [TOOL:name args] ─────────────────────────────────────

    private static List<ToolCallIntent> ParseBracketToolCalls(string text)
    {
        var results = new List<ToolCallIntent>();
        foreach (Match match in BracketToolCallRegex().Matches(text))
        {
            string name = match.Groups[1].Value.Trim();
            string args = match.Groups[2].Value.Trim();

            if (!string.IsNullOrWhiteSpace(name))
            {
                results.Add(new ToolCallIntent(name, args, ToolCallFormat.BracketLegacy));
            }
        }

        return results;
    }

    // ── JSON parsing helpers ─────────────────────────────────────────────────

    private static ToolCallIntent? ParseToolCallJson(string json, ToolCallFormat format)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return ParseToolCallElement(doc.RootElement, format);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ToolCallIntent? ParseToolCallElement(JsonElement element, ToolCallFormat format)
    {
        // Try direct {"name":"x","arguments":{...}} format
        if (element.TryGetProperty("name", out JsonElement nameEl))
        {
            string? name = nameEl.GetString();
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            string args = "{}";
            if (element.TryGetProperty("arguments", out JsonElement argsEl))
            {
                args = argsEl.ValueKind == JsonValueKind.String
                    ? argsEl.GetString() ?? "{}"
                    : argsEl.GetRawText();
            }
            else if (element.TryGetProperty("parameters", out JsonElement paramsEl))
            {
                args = paramsEl.GetRawText();
            }

            return new ToolCallIntent(name, args, format);
        }

        // Try OpenAI nested {"function":{"name":"x","arguments":{...}}} format
        if (element.TryGetProperty("function", out JsonElement funcEl))
        {
            return ParseToolCallElement(funcEl, format);
        }

        return null;
    }

    // ── Compiled regex patterns ──────────────────────────────────────────────

    [GeneratedRegex(@"<tool_call>\s*(.*?)\s*</tool_call>", RegexOptions.Singleline | RegexOptions.Compiled)]
    private static partial Regex XmlToolCallRegex();

    [GeneratedRegex(@"\{[\s]*""tool_calls""[\s]*:[\s]*\[.*?\][\s]*\}", RegexOptions.Singleline | RegexOptions.Compiled)]
    private static partial Regex JsonToolCallsRegex();

    [GeneratedRegex(@"```tool_call\s*\n(.*?)\n\s*```", RegexOptions.Singleline | RegexOptions.Compiled)]
    private static partial Regex MarkdownToolCallRegex();

    [GeneratedRegex(@"\[TOOL:([^\s\]]+)\s*([^\]]*)\]", RegexOptions.Compiled)]
    private static partial Regex BracketToolCallRegex();
}

/// <summary>
/// Represents a parsed tool call intent from LLM output.
/// </summary>
/// <param name="ToolName">The name of the tool to invoke.</param>
/// <param name="ArgumentsJson">The arguments as a JSON string (or plain text for bracket format).</param>
/// <param name="Format">The format in which the tool call was expressed.</param>
public sealed record ToolCallIntent(string ToolName, string ArgumentsJson, ToolCallFormat Format);

/// <summary>
/// The format in which an LLM expressed a tool call.
/// </summary>
public enum ToolCallFormat
{
    /// <summary>XML tag: &lt;tool_call&gt;{...}&lt;/tool_call&gt;</summary>
    XmlTag,

    /// <summary>JSON array: {"tool_calls":[...]}</summary>
    JsonToolCalls,

    /// <summary>Bracket: [TOOL:name args]</summary>
    BracketLegacy,

    /// <summary>Ollama /api/chat native tool response.</summary>
    NativeApi,

    /// <summary>Markdown code block: ```tool_call\n{...}\n```</summary>
    MarkdownBlock
}
