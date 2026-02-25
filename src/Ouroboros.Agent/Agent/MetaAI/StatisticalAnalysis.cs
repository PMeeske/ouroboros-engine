namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Statistical analysis of experiment results.
/// </summary>
public sealed record StatisticalAnalysis(
    double EffectSize,
    bool IsSignificant,
    string Interpretation);