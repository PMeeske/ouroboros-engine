namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for curiosity-driven behavior.
/// </summary>
public sealed record CuriosityEngineConfig(
    double ExplorationThreshold = 0.6,
    double ExploitationBias = 0.7,
    int MaxExplorationPerSession = 5,
    bool EnableSafeExploration = true,
    double MinSafetyScore = 0.8);