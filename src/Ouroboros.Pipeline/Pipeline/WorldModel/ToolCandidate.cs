namespace Ouroboros.Pipeline.WorldModel;

/// <summary>
/// Represents a scored candidate tool for selection.
/// </summary>
/// <param name="Tool">The candidate tool.</param>
/// <param name="FitScore">How well the tool fits the goal (0.0 to 1.0).</param>
/// <param name="CostScore">Normalized cost score (lower is better, 0.0 to 1.0).</param>
/// <param name="SpeedScore">Normalized speed score (higher is faster, 0.0 to 1.0).</param>
/// <param name="QualityScore">Expected quality score (0.0 to 1.0).</param>
/// <param name="MatchedCapabilities">Capabilities that matched the goal requirements.</param>
public sealed record ToolCandidate(
    ITool Tool,
    double FitScore,
    double CostScore,
    double SpeedScore,
    double QualityScore,
    IReadOnlyList<string> MatchedCapabilities)
{
    /// <summary>
    /// Calculates the combined score based on optimization strategy.
    /// </summary>
    /// <param name="strategy">The optimization strategy.</param>
    /// <returns>The combined weighted score.</returns>
    public double GetWeightedScore(OptimizationStrategy strategy)
    {
        return strategy switch
        {
            OptimizationStrategy.Cost => (FitScore * 0.4) + ((1.0 - CostScore) * 0.5) + (QualityScore * 0.1),
            OptimizationStrategy.Speed => (FitScore * 0.3) + (SpeedScore * 0.5) + (QualityScore * 0.2),
            OptimizationStrategy.Quality => (FitScore * 0.3) + (QualityScore * 0.6) + (SpeedScore * 0.1),
            OptimizationStrategy.Balanced => (FitScore * 0.4) + (QualityScore * 0.3) + (SpeedScore * 0.2) + ((1.0 - CostScore) * 0.1),
            _ => FitScore,
        };
    }

    /// <summary>
    /// Creates a candidate with default scores from a tool match.
    /// </summary>
    /// <param name="tool">The tool.</param>
    /// <param name="match">The tool match containing fit information.</param>
    /// <returns>A new tool candidate.</returns>
    public static ToolCandidate FromMatch(ITool tool, ToolMatch match)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(match);

        return new ToolCandidate(
            Tool: tool,
            FitScore: match.RelevanceScore,
            CostScore: 0.5, // Default normalized cost
            SpeedScore: 0.5, // Default speed
            QualityScore: match.RelevanceScore, // Use relevance as quality proxy
            MatchedCapabilities: match.MatchedCapabilities);
    }
}