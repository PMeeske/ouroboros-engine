using Ouroboros.Diagnostics;

namespace Ouroboros.Agent;

/// <summary>
/// A configured, ready-to-run agent that wraps a chat model and tool registry into a single-step or multi-step execution loop.
/// Instances are created via the agent builder; they cannot be constructed directly.
/// </summary>
public sealed class AgentInstance
{
    private readonly Ouroboros.Abstractions.Core.IChatCompletionModel _chat;
    private readonly ToolRegistry _tools;
    private readonly int _maxSteps;

    internal AgentInstance(string mode, Ouroboros.Abstractions.Core.IChatCompletionModel chat, ToolRegistry tools, int maxSteps)
    {
        Mode = string.IsNullOrWhiteSpace(mode) ? "simple" : mode;
        _chat = chat;
        _tools = tools;
        _maxSteps = Math.Max(1, maxSteps);
    }

    /// <summary>Gets the execution mode the agent was built with (e.g., <c>"simple"</c>, <c>"react"</c>).</summary>
    public string Mode { get; }

    /// <summary>Gets a value indicating whether verbose debug output is enabled for this agent.</summary>
    public bool Debug { get; init; }

    /// <summary>Gets a value indicating whether retrieval-augmented generation (RAG) is enabled.</summary>
    public bool RagEnabled { get; init; }

    /// <summary>Gets the name of the embedding model used when <see cref="RagEnabled"/> is <see langword="true"/>.</summary>
    public string EmbedModelName { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether tool calls are serialised as structured JSON.</summary>
    public bool JsonTools { get; init; }

    /// <summary>Gets a value indicating whether streaming output is requested from the underlying model.</summary>
    public bool Stream { get; init; }

    /// <summary>
    /// Runs the agent against <paramref name="prompt"/>, iterating up to the configured maximum step count.
    /// Each iteration dispatches any tool calls embedded in the model response before continuing.
    /// Returns early when the model output no longer contains the <c>[AGENT-CONTINUE]</c> sentinel.
    /// </summary>
    public async Task<string> RunAsync(string prompt, CancellationToken ct = default)
    {
        string current = prompt;
        List<string> history = new List<string>();
        for (int i = 0; i < _maxSteps; i++)
        {
            history.Add(current);
            Telemetry.RecordAgentIteration();
            string response = await _chat.GenerateTextAsync(current, ct).ConfigureAwait(false);
            (string text, List<ToolExecution> toolCalls) = await new ToolAwareChatModel(_chat, _tools).GenerateWithToolsAsync(response, ct).ConfigureAwait(false);
            foreach (ToolExecution call in toolCalls)
            {
                Telemetry.RecordAgentToolCalls(1);
                Telemetry.RecordToolName(call.ToolName);
            }
            current = text;
            if (!current.Contains("[AGENT-CONTINUE]", StringComparison.OrdinalIgnoreCase))
                return current;
        }
        Telemetry.RecordAgentRetry();
        return current;
    }
}