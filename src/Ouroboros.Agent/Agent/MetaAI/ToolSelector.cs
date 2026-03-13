// <copyright file="ToolSelector.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Text;
using System.Text.Json;
using Ouroboros.Pipeline.Prompts;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// LLM-based tool selector that uses structured prompts to select appropriate tools
/// and extract arguments from step descriptions.
/// </summary>
public sealed class ToolSelector
{
    private readonly IReadOnlyList<ITool> _tools;
    private readonly Ouroboros.Abstractions.Core.IChatCompletionModel _llm;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolSelector"/> class.
    /// </summary>
    /// <param name="tools">The list of available tools.</param>
    /// <param name="llm">The LLM for tool selection.</param>
    public ToolSelector(IReadOnlyList<ITool> tools, Ouroboros.Abstractions.Core.IChatCompletionModel llm)
    {
        ArgumentNullException.ThrowIfNull(tools);
        _tools = tools;
        ArgumentNullException.ThrowIfNull(llm);
        _llm = llm;
    }

    /// <summary>
    /// Selects an appropriate tool for the given step description using the LLM.
    /// </summary>
    /// <param name="stepDescription">The step description from the plan.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A ToolSelection if a tool is appropriate, null if no tool is needed.</returns>
    public async Task<ToolSelection?> SelectToolAsync(string stepDescription, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(stepDescription))
        {
            return null;
        }

        if (_tools.Count == 0)
        {
            return null;
        }

        // Build structured prompt with tool information
        string prompt = BuildToolSelectionPrompt(stepDescription);

        try
        {
            // Call LLM for tool selection
            string response = await _llm.GenerateTextAsync(prompt, ct);

            // Parse the LLM response
            return ParseToolSelectionResponse(response);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            // If LLM call fails, return null to allow fallback
            return null;
        }
    }

    /// <summary>
    /// Builds a structured prompt that lists available tools and asks the LLM to select one.
    /// </summary>
    private string BuildToolSelectionPrompt(string stepDescription)
    {
        var toolsBuilder = new StringBuilder();

        // List each tool with its description and JSON schema
        foreach (ITool tool in _tools)
        {
            toolsBuilder.AppendLine($"Tool: {tool.Name}");
            toolsBuilder.AppendLine($"Description: {tool.Description}");

            if (!string.IsNullOrWhiteSpace(tool.JsonSchema))
            {
                toolsBuilder.AppendLine($"Parameters Schema: {tool.JsonSchema}");
            }

            toolsBuilder.AppendLine();
        }

        return PromptTemplateLoader.GetPromptText("MetaAI", "ToolSelector")
            .Replace("{{$availableTools}}", toolsBuilder.ToString())
            .Replace("{{$stepDescription}}", stepDescription);
    }

    /// <summary>
    /// Parses the LLM response to extract tool selection and arguments.
    /// </summary>
    private static ToolSelection? ParseToolSelectionResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

        try
        {
            // Try to extract JSON from the response (handle cases where LLM adds extra text)
            string jsonContent = ExtractJsonFromResponse(response);

            using JsonDocument doc = JsonDocument.Parse(jsonContent);
            JsonElement root = doc.RootElement;

            // Check if "tool" property exists and is not null
            if (!root.TryGetProperty("tool", out JsonElement toolElement))
            {
                return null;
            }

            // If tool is null, no tool is needed
            if (toolElement.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            string? toolName = toolElement.GetString();
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return null;
            }

            // Extract arguments (if present)
            string argumentsJson = "{}";
            if (root.TryGetProperty("arguments", out JsonElement argsElement) && 
                argsElement.ValueKind == JsonValueKind.Object)
            {
                argumentsJson = argsElement.GetRawText();
            }

            return new ToolSelection(toolName, argumentsJson);
        }
        catch (JsonException)
        {
            // If JSON parsing fails, return null to allow fallback
            return null;
        }
    }

    /// <summary>
    /// Extracts JSON content from response that might have extra text.
    /// </summary>
    private static string ExtractJsonFromResponse(string response)
    {
        // Try to find JSON object boundaries
        int startIndex = response.IndexOf('{');
        int endIndex = response.LastIndexOf('}');

        if (startIndex >= 0 && endIndex > startIndex)
        {
            return response[startIndex..(endIndex + 1)];
        }

        return response.Trim();
    }
}
