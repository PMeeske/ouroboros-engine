// <copyright file="ToolAwareChatModel.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Subjects;

namespace Ouroboros.Providers;

/// <summary>
/// A chat model wrapper that can execute tools based on special tool invocation syntax in responses.
/// Uses monadic Result{T,E} for consistent error handling throughout the pipeline.
/// Supports thinking mode when the underlying model implements IThinkingChatModel.
/// </summary>
/// <remarks>
/// When a <see cref="McpToolCallParser"/> is provided, it is used as the primary parser
/// (supporting XML, JSON, markdown, and bracket formats). The legacy regex pattern
/// <c>[TOOL:name args]</c> remains as an ultimate fallback.
/// <para>
/// Integrates with <see cref="NeuralPathway"/> for health tracking and exposes an
/// <see cref="IObservable{T}"/> event stream for tool execution events, enabling
/// reactive composition with the rest of the Ouroboros pipeline.
/// </para>
/// </remarks>
/// <param name="llm">The underlying chat completion model.</param>
/// <param name="registry">The tool registry for tool execution.</param>
/// <param name="antlrParser">Optional ANTLR-based parser for multi-format tool call extraction.</param>
/// <param name="neuralPathway">Optional neural pathway for health tracking.</param>
public sealed class ToolAwareChatModel(
    Ouroboros.Abstractions.Core.IChatCompletionModel llm,
    ToolRegistry registry,
    McpToolCallParser? antlrParser = null,
    NeuralPathway? neuralPathway = null)
{
    private readonly Subject<ToolExecutionEvent> _toolExecutionSubject = new();

    /// <summary>
    /// Gets the underlying chat completion model.
    /// </summary>
    public Ouroboros.Abstractions.Core.IChatCompletionModel InnerModel => llm;

    /// <summary>
    /// Returns true if the underlying model supports thinking mode.
    /// </summary>
    public bool SupportsThinking => llm is IThinkingChatModel;

    /// <summary>
    /// Observable stream of tool execution events for reactive composition.
    /// </summary>
    /// <remarks>
    /// Subscribers receive <see cref="ToolExecutionEvent"/> for every tool invocation,
    /// enabling integration with metrics collectors, event sourcing, and the MeTTa AtomSpace.
    /// </remarks>
    public IObservable<ToolExecutionEvent> ToolExecutions => _toolExecutionSubject;

    /// <summary>
    /// When true, streaming tokens are piped through an SMCP token atomizer
    /// for mid-stream intent detection and tool self-selection.
    /// </summary>
    public bool SmcpMode { get; set; }

    /// <summary>
    /// Callback invoked for each streaming token when <see cref="SmcpMode"/> is enabled.
    /// Set by the app layer to feed tokens into the <c>SmcpTokenAtomizer</c>.
    /// </summary>
    public Action<string>? SmcpTokenCallback { get; set; }

    /// <summary>
    /// Generates a response and executes any tools mentioned in the response.
    /// </summary>
    /// <param name="prompt">The input prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple containing the final text and list of tool executions.</returns>
    public async Task<(string Text, List<ToolExecution> Tools)> GenerateWithToolsAsync(string prompt, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            string result = await llm.GenerateTextAsync(prompt, ct).ConfigureAwait(false);
            var response = await ProcessToolCallsAsync(result, ct).ConfigureAwait(false);

            sw.Stop();
            neuralPathway?.RecordActivation(sw.Elapsed);
            return response;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            sw.Stop();
            neuralPathway?.RecordInhibition();
            throw;
        }
    }

    /// <summary>
    /// Generates a response with thinking/reasoning content and executes any tools mentioned.
    /// </summary>
    /// <param name="prompt">The input prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple containing the thinking response and list of tool executions.</returns>
    public async Task<(ThinkingResponse Response, List<ToolExecution> Tools)> GenerateWithThinkingAndToolsAsync(string prompt, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            if (llm is not IThinkingChatModel thinkingModel)
            {
                // Fallback: use regular generation and wrap in ThinkingResponse
                string result = await llm.GenerateTextAsync(prompt, ct).ConfigureAwait(false);
                var (processedText, tools) = await ProcessToolCallsAsync(result, ct).ConfigureAwait(false);
                sw.Stop();
                neuralPathway?.RecordActivation(sw.Elapsed);
                return (new ThinkingResponse(null, processedText), tools);
            }

            ThinkingResponse response = await thinkingModel.GenerateWithThinkingAsync(prompt, ct).ConfigureAwait(false);

            // Process tool calls in the content only (not in thinking)
            var (processedContent, toolCalls) = await ProcessToolCallsAsync(response.Content, ct).ConfigureAwait(false);
            sw.Stop();
            neuralPathway?.RecordActivation(sw.Elapsed);

            return (response with { Content = processedContent }, toolCalls);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            sw.Stop();
            neuralPathway?.RecordInhibition();
            throw;
        }
    }

    /// <summary>
    /// Monadic version that returns Result{T,E} for better error handling.
    /// </summary>
    /// <param name="prompt">The input prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the response and tool executions, or an error.</returns>
    public async Task<Result<(string Text, List<ToolExecution> Tools), string>> GenerateWithToolsResultAsync(string prompt, CancellationToken ct = default)
    {
        try
        {
            (string text, List<ToolExecution> tools) = await this.GenerateWithToolsAsync(prompt, ct).ConfigureAwait(false);
            return Result<(string, List<ToolExecution>), string>.Success((text, tools));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<(string, List<ToolExecution>), string>.Failure($"Tool-aware generation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Monadic version with thinking support that returns Result{T,E}.
    /// </summary>
    /// <param name="prompt">The input prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the thinking response and tool executions, or an error.</returns>
    public async Task<Result<(ThinkingResponse Response, List<ToolExecution> Tools), string>> GenerateWithThinkingAndToolsResultAsync(string prompt, CancellationToken ct = default)
    {
        try
        {
            var result = await GenerateWithThinkingAndToolsAsync(prompt, ct).ConfigureAwait(false);
            return Result<(ThinkingResponse, List<ToolExecution>), string>.Success(result);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<(ThinkingResponse, List<ToolExecution>), string>.Failure($"Tool-aware generation with thinking failed: {ex.Message}");
        }
    }

    // ── Crush-inspired hooks ────────────────────────────────────────────────────

    /// <summary>
    /// Optional pre-execution gate called before every LLM-driven tool call.
    /// Return <c>false</c> to skip the tool (treated as denied).
    /// Signature: (toolName, args, cancellationToken) => Task&lt;bool&gt;
    /// </summary>
    public Func<string, string, CancellationToken, Task<bool>>? BeforeInvoke { get; set; }

    /// <summary>
    /// Optional post-execution callback for metrics, UI updates, and event publishing.
    /// Signature: (toolName, args, output, elapsed, success)
    /// </summary>
    public Action<string, string, string, TimeSpan, bool>? AfterInvoke { get; set; }

    // ── Internal execution ───────────────────────────────────────────────────────

    private static readonly System.Text.RegularExpressions.Regex ToolInvocationPattern =
        new(@"\[TOOL:([^\s]+)\s*([^\]]*)\]", System.Text.RegularExpressions.RegexOptions.Compiled);

    private async Task<(string Text, List<ToolExecution> Tools)> ProcessToolCallsAsync(string result, CancellationToken ct)
    {
        // SMCP: pipe response tokens through the atomizer for intent detection
        if (SmcpMode && SmcpTokenCallback != null && !string.IsNullOrEmpty(result))
        {
            var tokens = result.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
                SmcpTokenCallback(token);
        }

        List<ToolExecution> toolCalls = [];
        string modifiedResult = result;

        // Priority 1: ANTLR multi-format parser (XML, JSON, markdown, bracket)
        if (antlrParser is not null)
        {
            var intents = antlrParser.Parse(result);
            if (intents.Count > 0)
            {
                string cleanText = antlrParser.ExtractTextSegments(result);
                foreach (var intent in intents)
                {
                    string name = intent.ToolName;
                    string args = intent.ArgumentsJson;

                    ITool? tool = registry.Get(name);
                    if (tool is null)
                    {
                        cleanText += $" [TOOL-RESULT:{name}] error: tool not found";
                        PublishToolEvent(name, args, "error: tool not found", TimeSpan.Zero, false);
                        continue;
                    }

                    if (BeforeInvoke != null)
                    {
                        var allowed = await BeforeInvoke(name, args, ct).ConfigureAwait(false);
                        if (!allowed)
                        {
                            cleanText += $" [TOOL-RESULT:{name}] denied by user";
                            PublishToolEvent(name, args, "denied by user", TimeSpan.Zero, false);
                            continue;
                        }
                    }

                    string output;
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        Result<string, string> toolResult = await tool.InvokeAsync(args, ct).ConfigureAwait(false);
                        output = toolResult.Match(success => success, error => $"error: {error}");
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        output = $"error: {ex.Message}";
                    }
                    finally
                    {
                        sw.Stop();
                    }

                    bool success = !output.StartsWith("error:");
                    AfterInvoke?.Invoke(name, args, output, sw.Elapsed, success);
                    PublishToolEvent(name, args, output, sw.Elapsed, success);
                    toolCalls.Add(new ToolExecution(name, args, output, DateTime.UtcNow));
                }

                return (cleanText.Trim(), toolCalls);
            }
        }

        // Priority 2 (fallback): Legacy regex [TOOL:name args] pattern
        var matches = ToolInvocationPattern.Matches(result);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            string fullMatch = match.Value;
            string name = match.Groups[1].Value;
            string args = match.Groups[2].Value.Trim();

            // Validate tool name is not empty or just whitespace
            if (string.IsNullOrWhiteSpace(name))
            {
                string errorResult = "[TOOL-RESULT:?] error: empty tool name";
                modifiedResult = modifiedResult.Replace(fullMatch, errorResult);
                continue;
            }

            ITool? tool = registry.Get(name);
            if (tool is null)
            {
                string errorResult = $"[TOOL-RESULT:{name}] error: tool not found";
                modifiedResult = modifiedResult.Replace(fullMatch, errorResult);
                PublishToolEvent(name, args, "error: tool not found", TimeSpan.Zero, false);
                continue;
            }

            // ── Permission gate (Crush: [a]/[s]/[d] before every tool call) ──
            if (BeforeInvoke != null)
            {
                var allowed = await BeforeInvoke(name, args, ct).ConfigureAwait(false);
                if (!allowed)
                {
                    modifiedResult = modifiedResult.Replace(fullMatch, $"[TOOL-RESULT:{name}] denied by user");
                    PublishToolEvent(name, args, "denied by user", TimeSpan.Zero, false);
                    continue;
                }
            }

            string output;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Result<string, string> toolResult = await tool.InvokeAsync(args, ct).ConfigureAwait(false);
                output = toolResult.Match(
                    success => success,
                    error => $"error: {error}");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                output = $"error: {ex.Message}";
            }
            finally
            {
                sw.Stop();
            }

            bool success = !output.StartsWith("error:");

            // ── Post-execution hook (metrics, UI, event bus) ──
            AfterInvoke?.Invoke(name, args, output, sw.Elapsed, success);
            PublishToolEvent(name, args, output, sw.Elapsed, success);

            toolCalls.Add(new ToolExecution(name, args, output, DateTime.UtcNow));

            // Replace the tool invocation with the result in the text
            string replacement = $"[TOOL-RESULT:{name}] {output}";
            modifiedResult = modifiedResult.Replace(fullMatch, replacement);
        }

        return (modifiedResult, toolCalls);
    }

    private void PublishToolEvent(string toolName, string args, string output, TimeSpan elapsed, bool success)
    {
        _toolExecutionSubject.OnNext(new ToolExecutionEvent(toolName, args, output, elapsed, success, DateTime.UtcNow));
    }
}

/// <summary>
/// Event raised for each tool execution, enabling reactive stream composition.
/// </summary>
/// <param name="ToolName">The tool that was invoked.</param>
/// <param name="Arguments">The arguments passed to the tool.</param>
/// <param name="Output">The tool execution output.</param>
/// <param name="Elapsed">How long the tool execution took.</param>
/// <param name="Success">Whether the execution was successful.</param>
/// <param name="Timestamp">When the event occurred.</param>
public sealed record ToolExecutionEvent(
    string ToolName,
    string Arguments,
    string Output,
    TimeSpan Elapsed,
    bool Success,
    DateTime Timestamp);
