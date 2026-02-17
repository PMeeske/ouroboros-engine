#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace Ouroboros.Agent;

/// <summary>
/// Simplified agent harness. The historical version orchestrated complex
/// tool-aware planning. For the restored build we provide a compact,
/// deterministic implementation that keeps the public surface intact while
/// remaining fully synchronous.
/// </summary>
public static class AgentFactory
{
    public static AgentInstance Create(
        string mode,
        Ouroboros.Abstractions.Core.IChatCompletionModel chatModel,
        ToolRegistry tools,
        bool debug,
        int maxSteps,
        bool ragEnabled,
        string embedModelName,
        bool jsonTools,
        bool stream)
    {
        return new AgentInstance(mode, chatModel, tools, maxSteps)
        {
            Debug = debug,
            RagEnabled = ragEnabled,
            EmbedModelName = embedModelName,
            JsonTools = jsonTools,
            Stream = stream
        };
    }
}