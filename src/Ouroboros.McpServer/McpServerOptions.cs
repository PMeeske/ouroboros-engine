// <copyright file="McpServerOptions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.McpServer;

/// <summary>
/// Configuration options for the Ouroboros MCP server.
/// </summary>
public sealed class McpServerOptions
{
    /// <summary>
    /// Gets or sets the server name reported in the MCP initialize response.
    /// </summary>
    public string ServerName { get; set; } = "Ouroboros";

    /// <summary>
    /// Gets or sets the server version reported in the MCP initialize response.
    /// </summary>
    public string ServerVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the transport mode for the MCP server.
    /// </summary>
    public McpTransport Transport { get; set; } = McpTransport.Stdio;

    /// <summary>
    /// Gets or sets an optional filter restricting which tools are exposed.
    /// When null, all registered tools are exposed.
    /// </summary>
    public string[]? ToolFilter { get; set; }
}

/// <summary>
/// MCP transport mode.
/// </summary>
public enum McpTransport
{
    /// <summary>JSON-RPC 2.0 over stdin/stdout (standard MCP).</summary>
    Stdio,

    /// <summary>Server-Sent Events over HTTP.</summary>
    Sse,
}
