namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Internal state tracking for running experiments.
/// </summary>
internal sealed record ExperimentState(
    string ExperimentId,
    DateTime StartedAt);