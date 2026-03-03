// <copyright file="McpToolResult.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.McpServer;

/// <summary>
/// Represents the result of an MCP tool invocation.
/// </summary>
public sealed class McpToolResult
{
    /// <summary>
    /// Gets a value indicating whether the tool invocation resulted in an error.
    /// </summary>
    public bool IsError { get; private init; }

    /// <summary>
    /// Gets the text content of the result (either the success output or the error message).
    /// </summary>
    public string Content { get; private init; } = string.Empty;

    /// <summary>
    /// Creates a successful tool result.
    /// </summary>
    /// <param name="content">The tool output content.</param>
    /// <returns>A successful <see cref="McpToolResult"/>.</returns>
    public static McpToolResult Success(string content) =>
        new() { Content = content };

    /// <summary>
    /// Creates an error tool result.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>An error <see cref="McpToolResult"/>.</returns>
    public static McpToolResult Error(string message) =>
        new() { IsError = true, Content = message };
}
