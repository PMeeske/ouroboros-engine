namespace Ouroboros.Agent.NeuralSymbolic;

/// <summary>
/// Source of a symbolic rule.
/// </summary>
public enum RuleSource
{
    /// <summary>Rule extracted from a learned skill.</summary>
    ExtractedFromSkill,

    /// <summary>Rule learned from experience.</summary>
    LearnedFromExperience,

    /// <summary>Rule provided by user.</summary>
    UserProvided,

    /// <summary>Rule inferred from hypothesis.</summary>
    InferredFromHypothesis
}