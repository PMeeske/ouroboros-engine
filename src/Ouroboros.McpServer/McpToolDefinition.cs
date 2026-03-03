// <copyright file="McpToolDefinition.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Text.Json;

namespace Ouroboros.McpServer;

/// <summary>
/// Represents an MCP tool definition that maps to the Model Context Protocol schema.
/// </summary>
public sealed class McpToolDefinition
{
    /// <summary>
    /// Gets the unique name of the tool.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the human-readable description of the tool.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the JSON Schema describing the tool's input parameters.
    /// </summary>
    public JsonElement? InputSchema { get; init; }
}
