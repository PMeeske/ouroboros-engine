namespace Ouroboros.Pipeline.Branches;

/// <summary>
/// Statistics about recorded episodes in a branch.
/// </summary>
/// <param name="TotalEpisodes">Total number of episodes recorded.</param>
/// <param name="SuccessfulEpisodes">Number of successful episodes.</param>
/// <param name="SuccessRate">Ratio of successful to total episodes (0.0 to 1.0).</param>
/// <param name="AverageReward">Mean reward across all episodes.</param>
/// <param name="AverageSteps">Mean number of steps per episode.</param>
/// <param name="TotalReward">Sum of all episode rewards.</param>
public sealed record EpisodeStatistics(
    int TotalEpisodes,
    int SuccessfulEpisodes,
    double SuccessRate,
    double AverageReward,
    double AverageSteps,
    double TotalReward);