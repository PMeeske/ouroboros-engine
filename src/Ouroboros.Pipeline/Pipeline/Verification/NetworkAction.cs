namespace Ouroboros.Pipeline.Verification;

/// <summary>
/// Represents a network action.
/// </summary>
/// <param name="Operation">The operation type (e.g., "get", "post", "connect").</param>
/// <param name="Endpoint">The target endpoint for the operation.</param>
public sealed record NetworkAction(string Operation, string? Endpoint = null) : PlanAction
{
    /// <inheritdoc/>
    public override string ToMeTTaAtom() => $"(NetworkAction \"{Operation}\")";
}