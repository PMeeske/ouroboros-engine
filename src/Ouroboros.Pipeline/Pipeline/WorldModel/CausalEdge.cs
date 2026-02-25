namespace Ouroboros.Pipeline.WorldModel;

/// <summary>
/// Represents a directed edge in the causal graph indicating a cause-effect relationship.
/// </summary>
/// <param name="SourceId">The ID of the source (cause) node.</param>
/// <param name="TargetId">The ID of the target (effect) node.</param>
/// <param name="Strength">The strength/probability of the causal relationship (0.0 to 1.0).</param>
/// <param name="Condition">Optional condition that must be met for the causation to occur.</param>
public sealed record CausalEdge(
    Guid SourceId,
    Guid TargetId,
    double Strength,
    Option<string> Condition)
{
    /// <summary>
    /// Creates a new causal edge with no condition.
    /// </summary>
    /// <param name="sourceId">The source node ID.</param>
    /// <param name="targetId">The target node ID.</param>
    /// <param name="strength">The strength of the causal relationship.</param>
    /// <returns>A new causal edge.</returns>
    public static CausalEdge Create(Guid sourceId, Guid targetId, double strength)
    {
        double clampedStrength = Math.Clamp(strength, 0.0, 1.0);
        return new CausalEdge(sourceId, targetId, clampedStrength, Option<string>.None());
    }

    /// <summary>
    /// Creates a new causal edge with a condition.
    /// </summary>
    /// <param name="sourceId">The source node ID.</param>
    /// <param name="targetId">The target node ID.</param>
    /// <param name="strength">The strength of the causal relationship.</param>
    /// <param name="condition">The condition for the causation.</param>
    /// <returns>A new causal edge.</returns>
    public static CausalEdge Create(Guid sourceId, Guid targetId, double strength, string condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        double clampedStrength = Math.Clamp(strength, 0.0, 1.0);
        return new CausalEdge(sourceId, targetId, clampedStrength, Option<string>.Some(condition));
    }

    /// <summary>
    /// Creates a deterministic causal edge (strength = 1.0).
    /// </summary>
    /// <param name="sourceId">The source node ID.</param>
    /// <param name="targetId">The target node ID.</param>
    /// <returns>A deterministic causal edge.</returns>
    public static CausalEdge Deterministic(Guid sourceId, Guid targetId)
    {
        return new CausalEdge(sourceId, targetId, 1.0, Option<string>.None());
    }
}