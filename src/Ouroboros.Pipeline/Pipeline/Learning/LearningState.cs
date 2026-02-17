namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Represents the current state of the learning process.
/// </summary>
internal enum LearningState
{
    /// <summary>Initial exploration phase.</summary>
    Exploring,

    /// <summary>Learning is progressing toward convergence.</summary>
    Converging,

    /// <summary>Learning has converged to a stable solution.</summary>
    Converged,

    /// <summary>Learning is unstable and diverging.</summary>
    Diverging,

    /// <summary>Learning has stagnated with no improvement.</summary>
    Stagnant,
}