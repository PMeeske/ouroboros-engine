namespace Ouroboros.Pipeline.WorldModel;

/// <summary>
/// Represents a match between a tool and a goal with relevance scoring.
/// </summary>
/// <param name="ToolName">The name of the matched tool.</param>
/// <param name="RelevanceScore">Relevance score between 0.0 and 1.0.</param>
/// <param name="MatchedCapabilities">List of capabilities that matched the goal.</param>
public sealed record ToolMatch(
    string ToolName,
    double RelevanceScore,
    IReadOnlyList<string> MatchedCapabilities)
{
    /// <summary>
    /// Creates a tool match with no matched capabilities.
    /// </summary>
    /// <param name="toolName">The tool name.</param>
    /// <param name="score">The relevance score.</param>
    /// <returns>A new tool match.</returns>
    public static ToolMatch Create(string toolName, double score)
    {
        ArgumentNullException.ThrowIfNull(toolName);

        double clampedScore = Math.Clamp(score, 0.0, 1.0);
        return new ToolMatch(toolName, clampedScore, []);
    }

    /// <summary>
    /// Creates a tool match with matched capabilities.
    /// </summary>
    /// <param name="toolName">The tool name.</param>
    /// <param name="score">The relevance score.</param>
    /// <param name="capabilities">The matched capabilities.</param>
    /// <returns>A new tool match.</returns>
    public static ToolMatch Create(string toolName, double score, IEnumerable<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(toolName);
        ArgumentNullException.ThrowIfNull(capabilities);

        double clampedScore = Math.Clamp(score, 0.0, 1.0);
        return new ToolMatch(toolName, clampedScore, capabilities.ToList());
    }
}