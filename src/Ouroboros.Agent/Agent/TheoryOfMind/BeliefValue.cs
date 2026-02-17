namespace Ouroboros.Agent.TheoryOfMind;

/// <summary>
/// Represents a single belief value with associated metadata.
/// </summary>
/// <param name="Proposition">The belief proposition (e.g., "user_wants_help")</param>
/// <param name="Probability">Confidence in this belief (0.0 to 1.0)</param>
/// <param name="Source">Source of the belief (e.g., "observation", "inference")</param>
public sealed record BeliefValue(
    string Proposition,
    double Probability,
    string Source)
{
    /// <summary>
    /// Creates a belief from an observation.
    /// </summary>
    /// <param name="proposition">The belief proposition</param>
    /// <param name="probability">Confidence level</param>
    /// <returns>Belief value marked as observation</returns>
    public static BeliefValue FromObservation(string proposition, double probability = 1.0) =>
        new(proposition, probability, "observation");

    /// <summary>
    /// Creates a belief from inference.
    /// </summary>
    /// <param name="proposition">The belief proposition</param>
    /// <param name="probability">Confidence level</param>
    /// <returns>Belief value marked as inference</returns>
    public static BeliefValue FromInference(string proposition, double probability) =>
        new(proposition, probability, "inference");
}