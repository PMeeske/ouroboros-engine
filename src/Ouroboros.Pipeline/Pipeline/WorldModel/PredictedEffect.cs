namespace Ouroboros.Pipeline.WorldModel;

/// <summary>
/// Represents a predicted effect with its probability.
/// </summary>
/// <param name="Node">The node representing the predicted effect.</param>
/// <param name="Probability">The probability of this effect occurring (0.0 to 1.0).</param>
/// <param name="PathStrength">The cumulative strength along the causal path.</param>
public sealed record PredictedEffect(
    CausalNode Node,
    double Probability,
    double PathStrength);