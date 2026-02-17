namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Specifies the type of feedback received for learning.
/// </summary>
public enum FeedbackType
{
    /// <summary>
    /// Explicit feedback directly provided by a user or evaluator.
    /// </summary>
    Explicit,

    /// <summary>
    /// Implicit feedback inferred from user behavior (clicks, time spent, etc.).
    /// </summary>
    Implicit,

    /// <summary>
    /// Corrective feedback that includes the correct or preferred output.
    /// </summary>
    Corrective,

    /// <summary>
    /// Comparative feedback ranking multiple outputs.
    /// </summary>
    Comparative,
}