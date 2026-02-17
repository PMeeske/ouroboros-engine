namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Specifies the type of adaptation event that occurred.
/// </summary>
public enum AdaptationEventType
{
    /// <summary>
    /// The agent changed its overall strategy or approach.
    /// </summary>
    StrategyChange,

    /// <summary>
    /// Fine-tuning of parameters without changing the overall strategy.
    /// </summary>
    ParameterTune,

    /// <summary>
    /// Update to the underlying model or weights.
    /// </summary>
    ModelUpdate,

    /// <summary>
    /// Acquisition of a new skill or capability.
    /// </summary>
    SkillAcquisition,

    /// <summary>
    /// Reverting a previous adaptation due to performance degradation.
    /// </summary>
    Rollback,
}