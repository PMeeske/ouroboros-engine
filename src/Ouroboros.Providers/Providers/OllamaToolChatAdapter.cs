// <copyright file="OllamaToolChatAdapter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using Ouroboros.Abstractions.Core;
using Ouroboros.Providers.Resilience;
using Ouroboros.Tools;

namespace Ouroboros.Providers;

/// <summary>
/// Ollama adapter using the <c>/api/chat</c> endpoint with native tool definitions.
/// Falls back to <see cref="McpToolCallParser"/> for models that don't support native tools.
/// Wraps tool execution in <see cref="EvolutionaryRetryPolicy{TContext}"/> for adaptive retries.
/// </summary>
/// <remarks>
/// This adapter bridges the gap between:
/// <list type="bullet">
/// <item>MCP tool definitions (from <see cref="ToolRegistry"/>)</item>
/// <item>OllamaSharp's native <see cref="Tool"/> definitions</item>
/// <item>Semantic Kernel via <see cref="IChatClientBridge"/></item>
/// </list>
/// When the model supports native tool calling, tools are sent via <see cref="ChatRequest.Tools"/>.
/// When it doesn't, the response text is parsed via <see cref="McpToolCallParser"/> and
/// tool calls are extracted using the ANTLR-derived multi-format parser.
/// </remarks>
public sealed class OllamaToolChatAdapter : IChatCompletionModel, IChatClientBridge, IDisposable
{
    private readonly OllamaApiClient _client;
    private readonly string _model;
    private readonly ToolRegistry _tools;
    private readonly McpToolCallParser _parser;
    private readonly EvolutionaryRetryPolicy<ToolCallContext>? _retryPolicy;
    private readonly ILogger? _logger;
    private readonly ChatRuntimeSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaToolChatAdapter"/> class.
    /// </summary>
    /// <param name="endpoint">The Ollama API endpoint URL.</param>
    /// <param name="model">The model name (e.g. "mistral:latest").</param>
    /// <param name="tools">The tool registry containing available tools.</param>
    /// <param name="parser">The ANTLR-based tool call parser for fallback parsing.</param>
    /// <param name="retryPolicy">Optional evolutionary retry policy.</param>
    /// <param name="settings">Optional chat runtime settings.</param>
    /// <param name="apiKey">Optional API key for cloud endpoints.</param>
    /// <param name="logger">Optional logger.</param>
    public OllamaToolChatAdapter(
        string endpoint,
        string model,
        ToolRegistry tools,
        McpToolCallParser parser,
        EvolutionaryRetryPolicy<ToolCallContext>? retryPolicy = null,
        ChatRuntimeSettings? settings = null,
        string? apiKey = null,
        ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(parser);

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(endpoint.TrimEnd('/'), UriKind.Absolute),
            Timeout = TimeSpan.FromMinutes(10)
        };

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }

        _client = new OllamaApiClient(httpClient) { SelectedModel = model };
        _model = model;
        _tools = tools;
        _parser = parser;
        _retryPolicy = retryPolicy;
        _settings = settings ?? new ChatRuntimeSettings();
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new instance using a pre-configured <see cref="OllamaApiClient"/>.
    /// </summary>
    public OllamaToolChatAdapter(
        OllamaApiClient client,
        string model,
        ToolRegistry tools,
        McpToolCallParser parser,
        EvolutionaryRetryPolicy<ToolCallContext>? retryPolicy = null,
        ChatRuntimeSettings? settings = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(parser);

        _client = client;
        _model = model;
        _tools = tools;
        _parser = parser;
        _retryPolicy = retryPolicy;
        _settings = settings ?? new ChatRuntimeSettings();
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        var (text, _) = await GenerateWithToolsAsync(prompt, ct).ConfigureAwait(false);
        return text;
    }

    /// <summary>
    /// Generates a response with tool execution support.
    /// First tries native Ollama tool calling via <c>/api/chat</c>.
    /// Falls back to <see cref="McpToolCallParser"/> text parsing.
    /// Wraps the execution in <see cref="EvolutionaryRetryPolicy{TContext}"/> if available.
    /// </summary>
    /// <param name="prompt">The user prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple of the final response text and list of tool executions.</returns>
    public async Task<(string Text, List<ToolExecution> Tools)> GenerateWithToolsAsync(
        string prompt,
        CancellationToken ct = default)
    {
        if (_retryPolicy is not null)
        {
            var context = new ToolCallContext
            {
                Prompt = prompt,
                Tools = _tools.All.Select(t =>
                    new ToolDefinitionSlim(t.Name, t.Description, t.JsonSchema)).ToList(),
                Temperature = (float)_settings.Temperature
            };

            return await _retryPolicy.ExecuteWithEvolutionAsync(
                context,
                (ctx, innerCt) => ExecuteToolCallAsync(ctx.Prompt, ctx.Temperature, innerCt),
                ct).ConfigureAwait(false);
        }

        return await ExecuteToolCallAsync(prompt, (float)_settings.Temperature, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IChatClient GetChatClient() => _client;

    /// <inheritdoc/>
    public void Dispose()
    {
        _client.Dispose();
    }

    private async Task<(string Text, List<ToolExecution> Tools)> ExecuteToolCallAsync(
        string prompt,
        float temperature,
        CancellationToken ct)
    {
        var messages = new List<Message>
        {
            new(ChatRole.User, prompt)
        };

        // Build Ollama tool definitions from registry
        var ollamaTools = BuildOllamaTools();

        var sb = new StringBuilder();
        var toolExecutions = new List<ToolExecution>();
        int maxToolRounds = 5; // prevent infinite tool-call loops

        for (int round = 0; round < maxToolRounds; round++)
        {
            ct.ThrowIfCancellationRequested();

            var request = new ChatRequest
            {
                Model = _model,
                Messages = messages,
                Tools = ollamaTools.Count > 0 ? ollamaTools : null,
                Stream = false,
                Options = new OllamaSharp.Models.RequestOptions
                {
                    Temperature = temperature,
                    NumPredict = _settings.MaxTokens > 0 ? _settings.MaxTokens : null
                }
            };

            Message? assistantMessage = null;

            await foreach (var chunk in _client.ChatAsync(request, ct).ConfigureAwait(false))
            {
                if (chunk?.Message is not null)
                {
                    assistantMessage = chunk.Message;
                }
            }

            if (assistantMessage is null)
            {
                throw new InvalidOperationException($"Ollama model '{_model}' returned no response");
            }

            messages.Add(assistantMessage);

            // Check for native tool calls in the response
            var nativeToolCalls = assistantMessage.ToolCalls;
            if (nativeToolCalls is { Count: > 0 })
            {
                _logger?.LogDebug("Received {Count} native tool calls", nativeToolCalls.Count);

                foreach (var toolCall in nativeToolCalls)
                {
                    var execution = await ExecuteNativeToolCallAsync(toolCall, ct).ConfigureAwait(false);
                    toolExecutions.Add(execution);

                    // Feed tool result back as a tool message
                    messages.Add(new Message(ChatRole.Tool, execution.Output));
                }

                continue; // Let the model process the tool results
            }

            // No native tool calls — try fallback ANTLR parsing
            string responseText = assistantMessage.Content ?? string.Empty;

            var parsedIntents = _parser.Parse(responseText);
            if (parsedIntents.Count > 0)
            {
                _logger?.LogDebug("Parsed {Count} tool calls via ANTLR fallback", parsedIntents.Count);

                foreach (var intent in parsedIntents)
                {
                    var execution = await ExecuteParsedToolCallAsync(intent, ct).ConfigureAwait(false);
                    toolExecutions.Add(execution);
                }

                // Return cleaned text + tool results
                string cleanText = _parser.ExtractTextSegments(responseText);
                sb.Append(cleanText);
            }
            else
            {
                // No tool calls at all — pure text response
                sb.Append(responseText);
            }

            break; // No more tool call rounds needed
        }

        return (sb.ToString().Trim(), toolExecutions);
    }

    private async Task<ToolExecution> ExecuteNativeToolCallAsync(ToolCall toolCall, CancellationToken ct)
    {
        string toolName = toolCall.Function?.Name ?? "unknown";
        string args = toolCall.Function?.Arguments is not null
            ? JsonSerializer.Serialize(toolCall.Function.Arguments)
            : "{}";

        return await InvokeToolAsync(toolName, args, ct).ConfigureAwait(false);
    }

    private async Task<ToolExecution> ExecuteParsedToolCallAsync(ToolCallIntent intent, CancellationToken ct)
    {
        return await InvokeToolAsync(intent.ToolName, intent.ArgumentsJson, ct).ConfigureAwait(false);
    }

    private async Task<ToolExecution> InvokeToolAsync(string toolName, string args, CancellationToken ct)
    {
        ITool? tool = _tools.Get(toolName);
        if (tool is null)
        {
            _logger?.LogWarning("Tool '{ToolName}' not found in registry", toolName);
            return new ToolExecution(toolName, args, $"error: tool '{toolName}' not found", DateTime.UtcNow);
        }

        try
        {
            var result = await tool.InvokeAsync(args, ct).ConfigureAwait(false);
            string output = result.IsSuccess ? result.Value : $"error: {result.Error}";
            return new ToolExecution(toolName, args, output, DateTime.UtcNow);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Tool '{ToolName}' execution failed", toolName);
            return new ToolExecution(toolName, args, $"error: {ex.Message}", DateTime.UtcNow);
        }
    }

    private List<Tool> BuildOllamaTools()
    {
        var tools = new List<Tool>();
        foreach (ITool tool in _tools.All)
        {
            tools.Add(new Tool
            {
                Function = new Function
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    Parameters = ParseToolParameters(tool.JsonSchema)
                }
            });
        }

        return tools;
    }

    private static Parameters? ParseToolParameters(string? jsonSchema)
    {
        if (string.IsNullOrWhiteSpace(jsonSchema))
        {
            return new Parameters
            {
                Type = "object",
                Properties = new Dictionary<string, Property>()
            };
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonSchema);
            var root = doc.RootElement;

            var parameters = new Parameters
            {
                Type = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "object" : "object",
                Properties = new Dictionary<string, Property>(),
                Required = root.TryGetProperty("required", out var reqEl)
                    ? reqEl.EnumerateArray().Select(e => e.GetString()!).ToList()
                    : null
            };

            if (root.TryGetProperty("properties", out var propsEl))
            {
                foreach (var prop in propsEl.EnumerateObject())
                {
                    var property = new Property
                    {
                        Type = prop.Value.TryGetProperty("type", out var propType)
                            ? propType.GetString() ?? "string"
                            : "string",
                        Description = prop.Value.TryGetProperty("description", out var desc)
                            ? desc.GetString()
                            : null
                    };
                    parameters.Properties[prop.Name] = property;
                }
            }

            return parameters;
        }
        catch (JsonException)
        {
            return new Parameters
            {
                Type = "object",
                Properties = new Dictionary<string, Property>()
            };
        }
    }
}
