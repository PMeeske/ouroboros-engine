namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Adaptation strategy enumeration.
/// </summary>
public enum AdaptationStrategy
{
    Retry,
    ReplaceStep,
    AddStep,
    Replan,
    Abort
}