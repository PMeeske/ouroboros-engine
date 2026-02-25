namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a step that has been executed with its result.
/// </summary>
public sealed record ExecutedStep(
    string StepName,
    bool Success,
    TimeSpan Duration,
    Dictionary<string, object> Outputs);