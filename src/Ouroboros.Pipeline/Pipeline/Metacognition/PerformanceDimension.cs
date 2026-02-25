namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Represents the dimensions of performance that can be assessed.
/// Each dimension captures a distinct aspect of system capabilities.
/// </summary>
public enum PerformanceDimension
{
    /// <summary>
    /// Correctness and precision of outputs relative to ground truth.
    /// </summary>
    Accuracy,

    /// <summary>
    /// Response time and throughput efficiency.
    /// </summary>
    Speed,

    /// <summary>
    /// Novelty, originality, and divergent thinking in solutions.
    /// </summary>
    Creativity,

    /// <summary>
    /// Reliability and reproducibility of outputs across similar inputs.
    /// </summary>
    Consistency,

    /// <summary>
    /// Ability to handle new situations and transfer learning.
    /// </summary>
    Adaptability,

    /// <summary>
    /// Clarity, coherence, and effectiveness of communication.
    /// </summary>
    Communication,
}