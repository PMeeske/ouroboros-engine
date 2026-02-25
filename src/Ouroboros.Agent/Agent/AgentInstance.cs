using Ouroboros.Diagnostics;

namespace Ouroboros.Agent;

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

    public string Mode { get; }
    public bool Debug { get; init; }
    public bool RagEnabled { get; init; }
    public string EmbedModelName { get; init; } = string.Empty;
    public bool JsonTools { get; init; }
    public bool Stream { get; init; }

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