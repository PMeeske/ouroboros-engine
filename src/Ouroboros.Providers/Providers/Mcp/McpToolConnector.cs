// <copyright file="McpToolConnector.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Ouroboros.Tools;

namespace Ouroboros.Providers.Mcp;

/// <summary>
/// Connects Ouroboros to external MCP (Model Context Protocol) servers.
/// Discovers tools from MCP servers and exposes them as <see cref="ITool"/>
/// instances that can be registered in <see cref="ToolRegistry"/>.
/// </summary>
/// <remarks>
/// Supports two transports:
/// <list type="bullet">
/// <item><b>Stdio</b> — launches a subprocess (npx, uvx, custom executable) and communicates via JSON-RPC over stdin/stdout.</item>
/// <item><b>HTTP</b> — connects to a remote MCP server via HTTP POST/GET.</item>
/// </list>
/// After initialization, call <see cref="DiscoverToolsAsync"/> to get tool descriptors
/// and then <see cref="CreateTool"/> or <see cref="RegisterAllAsync"/> to add them
/// to a <see cref="ToolRegistry"/>.
/// <para>
/// This is the inverse of <see cref="Ouroboros.McpServer.ToolRegistryMcpBridge"/>:
/// that class exposes Ouroboros tools TO MCP clients; this class consumes
/// remote MCP tools and makes them available WITHIN Ouroboros.
/// </para>
/// </remarks>
public sealed class McpToolConnector : IDisposable
{
    private readonly string _serverName;
    private readonly McpTransport _transport;
    private readonly ILogger? _logger;
    private int _requestId;
    private bool _disposed;

    // Stdio transport fields
    private Process? _stdioProcess;
    private StreamWriter? _stdioWriter;
    private StreamReader? _stdioReader;
    private readonly SemaphoreSlim _stdioLock = new(1, 1);

    /// <summary>
    /// Creates an MCP connector using stdio transport.
    /// </summary>
    /// <param name="serverName"">Human-readable name for the MCP server.</param>
    /// <param name="command">Executable to run (e.g. "npx", "uvx", "python").</param>
    /// <param name="args">Arguments to pass to the command.</param>
    /// <param name="env">Additional environment variables for the subprocess.</param>
    /// <param name="logger">Optional logger.</param>
    public static McpToolConnector CreateStdio(
        string serverName,
        string command,
        IReadOnlyList<string>? args = null,
        IReadOnlyDictionary<string, string>? env = null,
        ILogger? logger = null)
    {
        var transport = new McpTransport(command, args ?? [], env ?? new Dictionary<string, string>());
        return new McpToolConnector(serverName, transport, logger);
    }

    /// <summary>
    /// Creates an MCP connector using HTTP transport.
    /// </summary>
    /// <param name="serverName">Human-readable name for the MCP server.</param>
    /// <param name="url">Base URL of the MCP server.</param>
    /// <param name="headers">HTTP headers (e.g. Authorization).</param>
    /// <param name="logger">Optional logger.</param>
    public static McpToolConnector CreateHttp(
        string serverName,
        string url,
        IReadOnlyDictionary<string, string>? headers = null,
        ILogger? logger = null)
    {
        var transport = new McpTransport(url, headers ?? new Dictionary<string, string>());
        return new McpToolConnector(serverName, transport, logger);
    }

    private McpToolConnector(string serverName, McpTransport transport, ILogger? logger)
    {
        _serverName = serverName;
        _transport = transport;
        _logger = logger;
    }

    /// <summary>
    /// Initializes the connection to the MCP server.
    /// For stdio: spawns the subprocess and performs JSON-RPC handshake.
    /// For HTTP: verifies connectivity with a health ping.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_transport.IsStdio)
        {
            await InitializeStdioAsync(ct).ConfigureAwait(false);
        }
        else
        {
            await InitializeHttpAsync(ct).ConfigureAwait(false);
        }

        _logger?.LogInformation("MCP server '{Server}' initialized", _serverName);
    }

    /// <summary>
    /// Discovers all tools exposed by the MCP server.
    /// </summary>
    /// <returns>List of tool descriptors with name, description, and JSON schema.</returns>
    public async Task<IReadOnlyList<McpToolDescriptor>> DiscoverToolsAsync(CancellationToken ct = default)
    {
        JsonNode? response = await SendJsonRpcAsync("tools/list", new JsonObject(), ct)
            .ConfigureAwait(false);

        if (response?["tools"] is not JsonArray tools)
        {
            return Array.Empty<McpToolDescriptor>();
        }

        var descriptors = new List<McpToolDescriptor>();
        foreach (JsonNode? tool in tools)
        {
            if (tool is null) continue;

            string name = tool["name"]?.GetValue<string>() ?? "unknown";
            string description = tool["description"]?.GetValue<string>() ?? string.Empty;
            string? schema = tool["inputSchema"]?.ToJsonString();

            descriptors.Add(new McpToolDescriptor(name, description, schema, this));
        }

        _logger?.LogInformation("Discovered {Count} tool(s) from MCP server '{Server}'",
            descriptors.Count, _serverName);

        return descriptors;
    }

    /// <summary>
    /// Creates an <see cref="ITool"/> wrapper for a discovered MCP tool.
    /// </summary>
    public ITool CreateTool(McpToolDescriptor descriptor)
    {
        return new McpTool(descriptor, this, _logger);
    }

    /// <summary>
    /// Discovers tools and registers them all into a <see cref="ToolRegistry"/>.
    /// </summary>
    /// <returns>New registry with MCP tools added.</returns>
    public async Task<ToolRegistry> RegisterAllAsync(ToolRegistry registry, CancellationToken ct = default)
    {
        IReadOnlyList<McpToolDescriptor> descriptors = await DiscoverToolsAsync(ct).ConfigureAwait(false);

        ToolRegistry result = registry;
        foreach (McpToolDescriptor descriptor in descriptors)
        {
            ITool tool = CreateTool(descriptor);
            result = result.WithTool(tool);
        }

        return result;
    }

    /// <summary>
    /// Invokes a tool on the MCP server via JSON-RPC.
    /// Internal method; called by <see cref="McpTool"/>.
    /// </summary>
    internal async Task<Result<string, string>> InvokeToolAsync(
        string toolName,
        string argumentsJson,
        CancellationToken ct = default)
    {
        var request = new JsonObject
        {
            ["name"] = toolName,
            ["arguments"] = JsonNode.Parse(argumentsJson) ?? new JsonObject(),
        };

        JsonNode? response = await SendJsonRpcAsync("tools/call", request, ct)
            .ConfigureAwait(false);

        if (response is null)
        {
            return Result<string, string>.Failure("MCP server returned null response");
        }

        if (response["isError"]?.GetValue<bool>() == true)
        {
            string error = response["content"]?.ToString() ?? "Unknown error";
            return Result<string, string>.Failure(error);
        }

        // MCP returns tool result as array of content objects; flatten to JSON string
        if (response["content"] is JsonArray contentArray)
        {
            var outputs = contentArray
                .Select(c => c?["text"]?.GetValue<string>() ?? c?.ToJsonString() ?? string.Empty)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            string combined = outputs.Count == 1
                ? outputs[0]
                : JsonSerializer.Serialize(outputs);

            return Result<string, string>.Success(combined);
        }

        return Result<string, string>.Success(response.ToJsonString());
    }

    // ── Transport initialization ────────────────────────────────────────────────

    private async Task InitializeStdioAsync(CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _transport.Command,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string arg in _transport.Args)
        {
            psi.ArgumentList.Add(arg);
        }

        foreach (var (key, value) in _transport.Env)
        {
            psi.Environment[key] = value;
        }

        _stdioProcess = Process.Start(psi);
        if (_stdioProcess is null)
        {
            throw new InvalidOperationException($"Failed to start MCP server: {_transport.Command}");
        }

        _stdioWriter = new StreamWriter(_stdioProcess.StandardInput.BaseStream, Encoding.UTF8);
        _stdioReader = new StreamReader(_stdioProcess.StandardOutput.BaseStream, Encoding.UTF8);

        // Wait briefly for process startup
        await Task.Delay(500, ct).ConfigureAwait(false);

        // Perform initialize handshake
        JsonObject initializeRequest = new()
        {
            ["jsonrpc"] = "2.0",
            ["id"] = GetNextId(),
            ["method"] = "initialize",
            ["params"] = new JsonObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject
                {
                    ["name"] = "ouroboros-mcp-client",
                    ["version"] = "1.0.0",
                },
            },
        };

        JsonNode? response = await SendStdioAsync(initializeRequest, ct).ConfigureAwait(false);
        if (response?["error"] is JsonObject error)
        {
            throw new InvalidOperationException($"MCP initialize failed: {error}");
        }

        // Send initialized notification
        JsonObject notification = new()
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "notifications/initialized",
        };
        await SendStdioAsync(notification, ct).ConfigureAwait(false);
    }

    private async Task InitializeHttpAsync(CancellationToken ct)
    {
        using var client = new HttpClient();
        foreach (var (key, value) in _transport.Headers)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(key, value);
        }

        // Health check: just verify the base URL responds
        try
        {
            var response = await client.GetAsync(_transport.Url, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"MCP HTTP server unreachable: {_transport.Url}", ex);
        }
    }

    // ── JSON-RPC dispatch ──────────────────────────────────────────────────────

    private async Task<JsonNode?> SendJsonRpcAsync(string method, JsonObject parameters, CancellationToken ct)
    {
        int id = GetNextId();
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters,
        };

        if (_transport.IsStdio)
        {
            return await SendStdioAsync(request, ct).ConfigureAwait(false);
        }
        else
        {
            return await SendHttpAsync(request, ct).ConfigureAwait(false);
        }
    }

    private async Task<JsonNode?> SendStdioAsync(JsonObject request, CancellationToken ct)
    {
        if (_stdioWriter is null || _stdioReader is null)
        {
            throw new InvalidOperationException("Stdio transport not initialized");
        }

        string json = request.ToJsonString(new JsonSerializerOptions { WriteIndented = false });

        await _stdioLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _stdioWriter.WriteLineAsync(json).ConfigureAwait(false);
            await _stdioWriter.FlushAsync(ct).ConfigureAwait(false);

            // Read response line
            string? responseLine = await _stdioReader.ReadLineAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(responseLine))
            {
                return null;
            }

            return JsonNode.Parse(responseLine);
        }
        finally
        {
            _stdioLock.Release();
        }
    }

    private async Task<JsonNode?> SendHttpAsync(JsonObject request, CancellationToken ct)
    {
        using var client = new HttpClient();
        foreach (var (key, value) in _transport.Headers)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(key, value);
        }

        using var content = new StringContent(
            request.ToJsonString(), Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync(_transport.Url, content, ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        string responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        return JsonNode.Parse(responseBody);
    }

    private int GetNextId() => Interlocked.Increment(ref _requestId);

    // ── IDisposable ─────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _stdioWriter?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { /* best effort */ }

        try
        {
            _stdioReader?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { /* best effort */ }

        try
        {
            if (_stdioProcess is { HasExited: false })
            {
                _stdioProcess.Kill();
                _stdioProcess.WaitForExit(TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception) { /* best effort */ }

        _stdioProcess?.Dispose();
        _stdioLock.Dispose();
    }
}

// ── Transport model ──────────────────────────────────────────────────────────

internal sealed class McpTransport
{
    public bool IsStdio { get; }
    public string Command { get; } = string.Empty;
    public IReadOnlyList<string> Args { get; } = [];
    public IReadOnlyDictionary<string, string> Env { get; } = new Dictionary<string, string>();
    public string Url { get; } = string.Empty;
    public IReadOnlyDictionary<string, string> Headers { get; } = new Dictionary<string, string>();

    public McpTransport(string command, IReadOnlyList<string> args, IReadOnlyDictionary<string, string> env)
    {
        IsStdio = true;
        Command = command;
        Args = args;
        Env = env;
    }

    public McpTransport(string url, IReadOnlyDictionary<string, string> headers)
    {
        IsStdio = false;
        Url = url;
        Headers = headers;
    }
}

/// <summary>
/// Descriptor for a tool discovered from an MCP server.
/// </summary>
public sealed record McpToolDescriptor(
    string Name,
    string Description,
    string? JsonSchema,
    McpToolConnector Source);

/// <summary>
/// Wraps an MCP server tool as an Ouroboros <see cref="ITool"/>.
/// </summary>
internal sealed class McpTool : ITool
{
    private readonly McpToolDescriptor _descriptor;
    private readonly McpToolConnector _connector;
    private readonly ILogger? _logger;

    public string Name => _descriptor.Name;
    public string Description => _descriptor.Description;
    public string? JsonSchema => _descriptor.JsonSchema;

    public McpTool(McpToolDescriptor descriptor, McpToolConnector connector, ILogger? logger)
    {
        _descriptor = descriptor;
        _connector = connector;
        _logger = logger;
    }

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        _logger?.LogDebug("Invoking MCP tool '{Tool}' with input: {Input}", Name, input);

        // Normalize input: if bare JSON string or plain text, wrap as arguments
        string argsJson = TryNormalizeArguments(input);

        return await _connector.InvokeToolAsync(Name, argsJson, ct).ConfigureAwait(false);
    }

    private static string TryNormalizeArguments(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "{}";
        }

        input = input.Trim();
        if (input.StartsWith('{') || input.StartsWith('['))
        {
            // Already JSON — validate by parsing
            try
            {
                JsonNode.Parse(input);
                return input;
            }
            catch (JsonException)
            {
                return $"{{\"input\":{JsonSerializer.Serialize(input)}}}";
            }
        }

        return $"{{\"input\":{JsonSerializer.Serialize(input)}}}";
    }
}
