// <copyright file="OnnxToolChatAdapter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Ouroboros.Abstractions.Core;
using Ouroboros.Providers;
using R3;
using Ouroboros.Tools;
using Ouroboros.Abstractions.Monads;

namespace Ouroboros.Providers.Onnx;

/// <summary>
/// Tool-aware wrapper for <see cref="OnnxGenAiChatModel"/>.
/// Injects Hermes-3-style tool definitions into prompts, parses
/// &lt;tool_call&gt; XML responses, executes tools from <see cref="ToolRegistry"/>,
/// and loops until the model returns plain text or max rounds reached.
/// </summary>
/// <remarks>
/// <para>
/// Hermes-3 supports tool calling via XML tags in its prompt template.
/// When tools are registered, the adapter prepends a tool-use instruction
/// and includes JSON schemas for each available tool. The model responds
/// with &lt;tool_call&gt; blocks that this adapter parses and dispatches.
/// </para>
/// <para>
/// Multi-turn: after executing a tool, the result is fed back into the
/// conversation as a user message, and generation continues. Max rounds
/// prevents infinite tool-call loops.
/// </para>
/// <para>
/// This design mirrors <see cref="OllamaToolChatAdapter"/> but uses prompt-level
/// schema injection instead of a native API, since ONNX Runtime GenAI has no
/// server-level tool support.
/// </para>
/// </remarks>
public sealed class OnnxToolChatAdapter : IOuroborosChatClient, IChatCompletionModel, ICostAwareChatModel, IDisposable
{
    private readonly OnnxGenAiChatModel _inner;
    private readonly Ouroboros.Tools.ToolRegistry _tools;
    private readonly McpToolCallParser _parser;
    private readonly ILogger? _logger;
    private readonly int _maxToolRounds;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnnxToolChatAdapter"/> class.
    /// </summary>
    /// <param name="inner">The underlying ONNX GenAI chat model.</param>
    /// <param name="tools">Tool registry containing available tools.</param>
    /// <param name="parser">Tool call parser for extracting XML/JSON tool calls.</param>
    /// <param name="maxToolRounds">Maximum tool-call rounds before forcing stop.</param>
    /// <param name="logger">Optional logger.</param>
    public OnnxToolChatAdapter(
        OnnxGenAiChatModel inner,
        Ouroboros.Tools.ToolRegistry tools,
        McpToolCallParser? parser = null,
        int maxToolRounds = 5,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(tools);

        _inner = inner;
        _tools = tools;
        _parser = parser ?? new McpToolCallParser();
        _maxToolRounds = maxToolRounds;
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool SupportsThinking => _inner.SupportsThinking;

    /// <inheritdoc/>
    public bool SupportsStreaming => _inner.SupportsStreaming;

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <inheritdoc/>
    public LlmCostTracker? CostTracker => _inner.CostTracker;

    /// <summary>
    /// Observable stream of tool executions from the last generation cycle.
    /// Subscribe before calling <see cref="GenerateWithToolsAsync"/> to capture all events.
    /// </summary>
    public Observable<ToolExecutionEvent> ToolExecutions => _toolExecutionSubject;
    private readonly Subject<ToolExecutionEvent> _toolExecutionSubject = new();

    /// <summary>
    /// Generates a response with tool execution support.
    /// </summary>
    /// <param name="prompt">User prompt text.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Final text + all tool executions.</returns>
    public async Task<(string Text, List<ToolExecution> Tools)> GenerateWithToolsAsync(
        string prompt,
        CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, prompt),
        };

        var toolExecutions = new List<ToolExecution>();
        var sb = new StringBuilder();
        int rounds = 0;

        while (rounds < _maxToolRounds)
        {
            ct.ThrowIfCancellationRequested();
            rounds++;

            // Inject tool schemas into system prompt
            var options = new ChatOptions
            {
                SystemPrompt = BuildToolSystemPrompt(),
            };

            _logger?.LogDebug("ONNX tool round {Round}/{Max}", rounds, _maxToolRounds);

            ChatResponse response = await _inner.GetResponseAsync(messages, options, ct)
                .ConfigureAwait(false);

            string responseText = response.Message.Text ?? string.Empty;

            // Parse tool calls from the response
            var intents = _parser.Parse(responseText);
            if (intents.Count == 0)
            {
                // No tool calls — return the text
                sb.Append(responseText);
                break;
            }

            _logger?.LogDebug("Detected {Count} tool call(s)", intents.Count);

            // Execute each tool call
            foreach (var intent in intents)
            {
                var execution = await ExecuteToolAsync(intent, ct).ConfigureAwait(false);
                toolExecutions.Add(execution);
                _toolExecutionSubject.OnNext(new ToolExecutionEvent(
                    execution.ToolName, execution.Arguments, execution.Output,
                    TimeSpan.Zero, execution.Success, DateTime.UtcNow));

                // Feed tool result back as a user message
                messages.Add(new ChatMessage(ChatRole.User,
                    $"[TOOL-RESULT:{execution.ToolName}] {execution.Output}"));
            }

            // Feed the model's original text (minus tool calls) as assistant context
            string cleanText = _parser.ExtractTextSegments(responseText);
            if (!string.IsNullOrWhiteSpace(cleanText))
            {
                messages.Add(new ChatMessage(ChatRole.Assistant, cleanText));
            }
        }

        if (rounds >= _maxToolRounds)
        {
            _logger?.LogWarning("Max tool rounds ({Max}) reached; forcing stop.", _maxToolRounds);
            sb.Append("\n[Note: tool execution limit reached.]");
        }

        return (sb.ToString().Trim(), toolExecutions);
    }

    /// <inheritdoc/>
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        string prompt = ExtractLastUserMessage(messages) ?? string.Empty;
        var (text, _) = await GenerateWithToolsAsync(prompt, cancellationToken).ConfigureAwait(false);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // ONNX GenAI with tools doesn't support clean streaming because tool parsing
        // needs the complete response. Delegate to non-streaming and yield at end.
        return StreamWithToolsFallbackAsync(messages, options, cancellationToken);
    }

    private async IAsyncEnumerable<ChatResponseUpdate> StreamWithToolsFallbackAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken ct)
    {
        string prompt = ExtractLastUserMessage(messages) ?? string.Empty;
        var (text, _) = await GenerateWithToolsAsync(prompt, ct).ConfigureAwait(false);

        // Yield as a single update (tools require full context before parsing)
        yield return new ChatResponseUpdate(ChatRole.Assistant, text);
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        var (text, _) = await GenerateWithToolsAsync(prompt, ct).ConfigureAwait(false);
        return text;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _inner.Dispose();
        _toolExecutionSubject.Dispose();
    }

    // ── Tool execution ──────────────────────────────────────────────────────────

    private async Task<ToolExecution> ExecuteToolAsync(ToolCallIntent intent, CancellationToken ct)
    {
        string toolName = intent.ToolName;
        string args = intent.ArgumentsJson;

        ITool? tool = _tools.Get(toolName);
        if (tool is null)
        {
            _logger?.LogWarning("Tool '{Tool}' not found in registry", toolName);
            return new ToolExecution(toolName, args, $"error: tool '{toolName}' not found", DateTime.UtcNow, false);
        }

        var sw = Stopwatch.StartNew();
        try
        {
            Result<string, string> result = await tool.InvokeAsync(args, ct).ConfigureAwait(false);
            sw.Stop();

            bool success = result.IsSuccess;
            string output = success ? result.Value : $"error: {result.Error}";
            return new ToolExecution(toolName, args, output, DateTime.UtcNow, success);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.LogError(ex, "Tool '{Tool}' execution failed", toolName);
            return new ToolExecution(toolName, args, $"error: {ex.Message}", DateTime.UtcNow, false);
        }
    }

    // ── Prompt composition ──────────────────────────────────────────────────────

    private string BuildToolSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You have access to the following tools. Use XML tags to call them:");
        sb.AppendLine("<tool_call>{\"name\":\"tool_name\",\"arguments\":{...}}</tool_call>");
        sb.AppendLine("You may call multiple tools. After tool results are provided, respond naturally.");
        sb.AppendLine();

        foreach (ITool tool in _tools.All)
        {
            sb.AppendLine($"Tool: {tool.Name}");
            sb.AppendLine($"Description: {tool.Description}");
            if (!string.IsNullOrEmpty(tool.JsonSchema))
            {
                sb.AppendLine($"Parameters: {tool.JsonSchema}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string? ExtractLastUserMessage(IEnumerable<ChatMessage> messages)
    {
        return messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text;
    }
}
