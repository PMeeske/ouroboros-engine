namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a tool recommendation with relevance scoring.
/// </summary>
public sealed record ToolRecommendation(
    string ToolName,
    string Description,
    double RelevanceScore,
    ToolCategory Category)
{
    /// <summary>Whether the tool is highly recommended (score > 0.7).</summary>
    public bool IsHighlyRecommended => RelevanceScore > 0.7;

    /// <summary>Whether the tool is recommended (score > 0.4).</summary>
    public bool IsRecommended => RelevanceScore > 0.4;
}