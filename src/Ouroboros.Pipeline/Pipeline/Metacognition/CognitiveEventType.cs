namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Represents the type of cognitive event occurring in the system.
/// Each type captures a distinct aspect of cognitive processing.
/// </summary>
public enum CognitiveEventType
{
    /// <summary>
    /// A new thought or idea has been generated.
    /// </summary>
    ThoughtGenerated,

    /// <summary>
    /// A decision has been made by the system.
    /// </summary>
    DecisionMade,

    /// <summary>
    /// An error has been detected in processing.
    /// </summary>
    ErrorDetected,

    /// <summary>
    /// Confusion or uncertainty has been sensed in processing.
    /// </summary>
    ConfusionSensed,

    /// <summary>
    /// A new insight or understanding has been gained.
    /// </summary>
    InsightGained,

    /// <summary>
    /// Attention has shifted to a new focus.
    /// </summary>
    AttentionShift,

    /// <summary>
    /// A goal has been activated for pursuit.
    /// </summary>
    GoalActivated,

    /// <summary>
    /// A goal has been successfully completed.
    /// </summary>
    GoalCompleted,

    /// <summary>
    /// High uncertainty detected in processing.
    /// </summary>
    Uncertainty,

    /// <summary>
    /// A contradiction has been detected in reasoning.
    /// </summary>
    Contradiction,
}