namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Represents the type of a reasoning step in a logical chain.
/// Each type captures a distinct cognitive operation in the reasoning process.
/// </summary>
public enum ReasoningStepType
{
    /// <summary>
    /// Direct perception or data gathering from available information.
    /// </summary>
    Observation,

    /// <summary>
    /// Logical derivation from observations or prior inferences.
    /// </summary>
    Inference,

    /// <summary>
    /// A tentative explanation or prediction to be validated.
    /// </summary>
    Hypothesis,

    /// <summary>
    /// Testing or verification of a hypothesis or inference.
    /// </summary>
    Validation,

    /// <summary>
    /// Modification of prior reasoning based on new evidence.
    /// </summary>
    Revision,

    /// <summary>
    /// An accepted premise without direct evidence.
    /// </summary>
    Assumption,

    /// <summary>
    /// A final determination reached through reasoning.
    /// </summary>
    Conclusion,

    /// <summary>
    /// Detection of inconsistency between reasoning steps.
    /// </summary>
    Contradiction,
}