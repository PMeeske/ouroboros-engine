// <copyright file="OnnxToolAgentProvider.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Agent.MeTTaAgents;
using Ouroboros.Providers;
using Ouroboros.Providers.Configuration;
using Ouroboros.Providers.Mcp;
using Ouroboros.Providers.Onnx;
using Ouroboros.Tools;
using Microsoft.Extensions.Logging;
using Ouroboros.Abstractions.Monads;

namespace Ouroboros.Agent.MeTTaAgents;

/// <summary>
/// Creates tool-aware ONNX Runtime GenAI agents from MeTTa definitions.
/// Wraps <see cref="OnnxGenAiChatModel"/> with <see cref="OnnxToolChatAdapter"/>
/// and optionally connects MCP servers for external tool access.
/// </summary>
/// <remarks>
/// Environment variables:
/// <list type="bullet">
/// <item><c>OUROBOROS_ONNX_MODEL_PATH</c> — base directory for ONNX models.</item>
/// <item><c>ONNX_USE_GPU</c> — 1 for DirectML, 0 for CPU.</item>
/// <item><c>MCP_SERVERS</c> — comma-separated MCP server names to auto-connect
/// (e.g. "time,filesystem").</item>
/// </list>
/// </remarks>
public sealed class OnnxToolAgentProvider : IAgentProviderFactory
{
    private readonly string _defaultModelBasePath;
    private readonly ToolRegistry _tools;
    private readonly List<McpToolConnector> _mcpServers;
    private readonly ILogger? _logger;

    /// <summary>
    /// Provider name this factory handles.
    /// </summary>
    public const string ProviderName = "OnnxTool";

    /// <summary>
    /// Creates a new tool-aware ONNX GenAI agent provider.
    /// </summary>
    /// <param name="defaultModelBasePath">Root directory containing ONNX model folders.</param>
    /// <param name="tools">Tool registry for this provider. Uses <see cref="ToolRegistry.CreateDefault"/> if null.</param>
    /// <param name="mcpServers">Pre-configured MCP server connectors.</param>
    /// <param name="logger">Optional logger.</param>
    public OnnxToolAgentProvider(
        string? defaultModelBasePath = null,
        ToolRegistry? tools = null,
        IEnumerable<McpToolConnector>? mcpServers = null,
        ILogger? logger = null)
    {
        _defaultModelBasePath = ResolveModelBasePath(defaultModelBasePath);
        _tools = tools ?? ToolRegistry.CreateDefault();
        _mcpServers = mcpServers?.ToList() ?? new List<McpToolConnector>();
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool CanHandle(string provider) =>
        string.Equals(provider, ProviderName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, "OnnxGenAI", StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, "Onnx", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task<Result<IChatCompletionModel, string>> CreateModelAsync(
        MeTTaAgentDef agentDef,
        CancellationToken ct = default)
    {
        string modelPath = ResolveModelPath(agentDef.Model ?? "hermes-3-llama-3.1-8b-int4");

        if (!Directory.Exists(modelPath))
        {
            string error = $"ONNX model directory not found: {modelPath}. "
                + "Set OUROBOROS_ONNX_MODEL_PATH or provide an absolute path. "
                + "Export via: optimum-cli export onnx --model NousResearch/Hermes-3-Llama-3.1-8B "
                + $"--dtype int4 --task text-generation-with-past {modelPath}";
            return Result<IChatCompletionModel, string>.Failure(error);
        }

        try
        {
            // Connect MCP servers and enrich tool registry
            ToolRegistry tools = await EnrichWithMcpToolsAsync(ct).ConfigureAwait(false);

            var onnxModel = new OnnxGenAiChatModel(modelPath);
            var parser = new McpToolCallParser();
            var toolAdapter = new OnnxToolChatAdapter(onnxModel, tools, parser, logger: _logger);

            return Result<IChatCompletionModel, string>.Success(toolAdapter);
        }
        catch (Exception ex) when (ex is OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<IChatCompletionModel, string>.Failure(
                $"Failed to create tool-aware ONNX model: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds an MCP server connector to this provider.
    /// Must be called before <see cref="CreateModelAsync"/>.
    /// </summary>
    public OnnxToolAgentProvider AddMcpServer(McpToolConnector connector)
    {
        _mcpServers.Add(connector);
        return this;
    }

    // ── Private helpers ─────────────────────────────────────────────────────────

    private async Task<ToolRegistry> EnrichWithMcpToolsAsync(CancellationToken ct)
    {
        ToolRegistry tools = _tools;

        foreach (McpToolConnector server in _mcpServers)
        {
            try
            {
                tools = await server.RegisterAllAsync(tools, ct).ConfigureAwait(false);
                _logger?.LogInformation("MCP server '{Server}' registered", server);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to register MCP server tools");
            }
        }

        return tools;
    }

    private string ResolveModelPath(string modelNameOrPath)
    {
        if (Path.IsPathRooted(modelNameOrPath))
        {
            return modelNameOrPath;
        }

        return Path.Combine(_defaultModelBasePath, modelNameOrPath);
    }

    private static string ResolveModelBasePath(string? fallback)
    {
        string? envPath = Environment.GetEnvironmentVariable("OUROBOROS_ONNX_MODEL_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            return Path.GetFullPath(envPath);
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return Path.GetFullPath(fallback);
        }

        // Default: user's Models directory
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, "Models", "onnx");
    }
}

/// <summary>
/// Extension methods for registering tool-aware ONNX providers.
/// </summary>
public static class OnnxToolAgentProviderExtensions
{
    /// <summary>
    /// Adds the tool-aware ONNX GenAI provider to a list of agent factories.
    /// </summary>
    public static IReadOnlyList<IAgentProviderFactory> AddOnnxTool(
        this IReadOnlyList<IAgentProviderFactory> factories,
        string? modelBasePath = null,
        ToolRegistry? tools = null,
        IEnumerable<McpToolConnector>? mcpServers = null)
    {
        var list = factories.ToList();
        list.Add(new OnnxToolAgentProvider(modelBasePath, tools, mcpServers));
        return list;
    }
}
