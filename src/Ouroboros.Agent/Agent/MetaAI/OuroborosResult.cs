namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Result of an Ouroboros orchestration cycle.
/// </summary>
/// <param name="Goal">The goal that was pursued.</param>
/// <param name="Success">Whether the cycle was successful.</param>
/// <param name="Output">The final output.</param>
/// <param name="PhaseResults">Results from each phase.</param>
/// <param name="CycleCount">Total improvement cycles completed.</param>
/// <param name="CurrentPhase">Current phase after execution.</param>
/// <param name="SelfReflection">Self-reflection summary.</param>
/// <param name="Duration">Total execution duration.</param>
/// <param name="Metadata">Additional metadata.</param>
public sealed record OuroborosResult(
    string Goal,
    bool Success,
    string Output,
    IReadOnlyList<PhaseResult> PhaseResults,
    int CycleCount,
    ImprovementPhase CurrentPhase,
    string SelfReflection,
    TimeSpan Duration,
    Dictionary<string, object> Metadata);