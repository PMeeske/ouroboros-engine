// <copyright file="ToolRegistryMcpBridge.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Text.Json;
using Ouroboros.Abstractions.Monads;

namespace Ouroboros.McpServer;

/// <summary>
/// Bridges the Ouroboros <see cref="ToolRegistry"/> to MCP tool definitions.
/// Each registered <see cref="ITool"/> becomes an MCP-callable tool.
/// </summary>
public static class ToolRegistryMcpBridge
{
    /// <summary>
    /// Converts all tools in the registry to MCP tool definitions.
    /// Returns a list conforming to the MCP tools/list response schema.
    /// </summary>
    /// <param name="registry">The tool registry containing registered tools.</param>
    /// <param name="filter">Optional name filter. When non-empty, only tools matching these names are included.</param>
    /// <returns>A read-only list of <see cref="McpToolDefinition"/> instances.</returns>
    public static IReadOnlyList<McpToolDefinition> ToMcpTools(
        ToolRegistry registry,
        string[]? filter = null)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var tools = new List<McpToolDefinition>();
        foreach (ITool tool in registry.All)
        {
            if (filter is { Length: > 0 }
                && !filter.Contains(tool.Name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            tools.Add(new McpToolDefinition
            {
                Name = tool.Name,
                Description = tool.Description,
                InputSchema = ParseSchema(tool.JsonSchema),
            });
        }

        return tools;
    }

    /// <summary>
    /// Invokes an MCP tool call by dispatching to the corresponding <see cref="ITool"/>
    /// in the registry.
    /// </summary>
    /// <param name="registry">The tool registry to look up the tool in.</param>
    /// <param name="toolName">The name of the tool to invoke.</param>
    /// <param name="arguments">Optional JSON arguments string.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An <see cref="McpToolResult"/> with the tool's output or an error.</returns>
    public static async Task<McpToolResult> InvokeToolAsync(
        ToolRegistry registry,
        string toolName,
        string? arguments,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(toolName);

        Option<ITool> toolOpt = registry.GetTool(toolName);

        if (!toolOpt.HasValue || toolOpt.Value is null)
        {
            return McpToolResult.Error($"Tool '{toolName}' not found.");
        }

        Result<string, string> result = await toolOpt.Value
            .InvokeAsync(arguments ?? string.Empty, ct)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? McpToolResult.Success(result.Value)
            : McpToolResult.Error(result.Error);
    }

    private static readonly JsonElement EmptyObjectSchema =
        JsonSerializer.Deserialize<JsonElement>("""{"type":"object","properties":{}}""");

    private static JsonElement ParseSchema(string? jsonSchema)
    {
        if (string.IsNullOrWhiteSpace(jsonSchema))
        {
            return EmptyObjectSchema;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(jsonSchema);
        }
        catch (JsonException)
        {
            return EmptyObjectSchema;
        }
    }
}
