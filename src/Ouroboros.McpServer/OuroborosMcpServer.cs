// <copyright file="OuroborosMcpServer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Text.Json;

namespace Ouroboros.McpServer;

/// <summary>
/// Stdio-based MCP server that exposes Ouroboros tools via the Model Context Protocol.
/// Handles JSON-RPC 2.0 messages over stdin/stdout.
/// </summary>
public sealed class OuroborosMcpServer
{
    private readonly ToolRegistry _registry;
    private readonly McpServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="OuroborosMcpServer"/> class.
    /// </summary>
    /// <param name="registry">The tool registry containing tools to expose.</param>
    /// <param name="options">Optional server configuration. Defaults are used when null.</param>
    public OuroborosMcpServer(ToolRegistry registry, McpServerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        _options = options ?? new McpServerOptions();
    }

    /// <summary>
    /// Runs the MCP server, reading JSON-RPC requests from the provided streams.
    /// </summary>
    /// <param name="input">The input stream to read JSON-RPC requests from.</param>
    /// <param name="output">The output stream to write JSON-RPC responses to.</param>
    /// <param name="ct">Cancellation token to stop the server.</param>
    public async Task RunAsync(Stream input, Stream output, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        using var reader = new StreamReader(input, leaveOpen: true);
        using var writer = new StreamWriter(output, leaveOpen: true) { AutoFlush = true };

        await RunCoreAsync(reader, writer, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs the MCP server using stdin/stdout as the transport.
    /// </summary>
    /// <param name="ct">Cancellation token to stop the server.</param>
    public async Task RunAsync(CancellationToken ct = default)
    {
        using var reader = new StreamReader(Console.OpenStandardInput());
        using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

        await RunCoreAsync(reader, writer, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles a single JSON-RPC message and returns the response (or null for notifications).
    /// Exposed for testing purposes.
    /// </summary>
    /// <param name="json">The raw JSON-RPC message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The JSON-RPC response string, or null for notifications.</returns>
    public async Task<string?> HandleMessageAsync(string json, CancellationToken ct = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string? method = root.TryGetProperty("method", out var m)
                ? m.GetString()
                : null;

            JsonElement? id = root.TryGetProperty("id", out var idElem)
                ? idElem.Clone()
                : null;

            return method switch
            {
                "initialize" => CreateInitializeResponse(id),
                "tools/list" => CreateToolListResponse(id),
                "tools/call" => await CreateToolCallResponseAsync(root, id, ct)
                    .ConfigureAwait(false),
                "notifications/initialized" => null,
                "ping" => CreatePongResponse(id),
                _ => CreateErrorResponse(id, -32601, $"Method not found: {method}"),
            };
        }
        catch (JsonException)
        {
            return CreateErrorResponse(null, -32700, "Parse error");
        }
    }

    private async Task RunCoreAsync(
        StreamReader reader,
        StreamWriter writer,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
            {
                break; // EOF
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string? response = await HandleMessageAsync(line, ct).ConfigureAwait(false);
            if (response is not null)
            {
                await writer.WriteLineAsync(response.AsMemory(), ct).ConfigureAwait(false);
            }
        }
    }

    private string CreateInitializeResponse(JsonElement? id)
    {
        var response = new
        {
            jsonrpc = "2.0",
            id,
            result = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new
                {
                    tools = new { listChanged = false },
                },
                serverInfo = new
                {
                    name = _options.ServerName,
                    version = _options.ServerVersion,
                },
            },
        };

        return JsonSerializer.Serialize(response);
    }

    private string CreateToolListResponse(JsonElement? id)
    {
        var tools = ToolRegistryMcpBridge.ToMcpTools(_registry, _options.ToolFilter);
        var toolDefs = tools.Select(t => new
        {
            name = t.Name,
            description = t.Description,
            inputSchema = t.InputSchema,
        });

        var response = new
        {
            jsonrpc = "2.0",
            id,
            result = new { tools = toolDefs },
        };

        return JsonSerializer.Serialize(response);
    }

    private async Task<string> CreateToolCallResponseAsync(
        JsonElement root,
        JsonElement? id,
        CancellationToken ct)
    {
        if (!root.TryGetProperty("params", out var paramsElem))
        {
            return CreateErrorResponse(id, -32602, "Missing 'params' in tools/call request.");
        }

        if (!paramsElem.TryGetProperty("name", out var nameElem))
        {
            return CreateErrorResponse(id, -32602, "Missing 'name' in tools/call params.");
        }

        string toolName = nameElem.GetString() ?? string.Empty;
        string? arguments = paramsElem.TryGetProperty("arguments", out var args)
            ? args.GetRawText()
            : null;

        McpToolResult result = await ToolRegistryMcpBridge
            .InvokeToolAsync(_registry, toolName, arguments, ct)
            .ConfigureAwait(false);

        var response = new
        {
            jsonrpc = "2.0",
            id,
            result = new
            {
                content = new[] { new { type = "text", text = result.Content } },
                isError = result.IsError,
            },
        };

        return JsonSerializer.Serialize(response);
    }

    private static string CreatePongResponse(JsonElement? id)
    {
        return JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            result = new { },
        });
    }

    private static string CreateErrorResponse(JsonElement? id, int code, string message)
    {
        return JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            error = new { code, message },
        });
    }
}
