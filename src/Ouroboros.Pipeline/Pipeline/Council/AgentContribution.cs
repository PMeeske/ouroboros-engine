namespace Ouroboros.Pipeline.Council;

/// <summary>
/// Represents a contribution from an agent during a debate round.
/// </summary>
/// <param name="AgentName">Name of the contributing agent.</param>
/// <param name="Content">The content of the contribution.</param>
/// <param name="TargetAgent">Optional target agent (for direct responses).</param>
/// <param name="ReferencedContributions">IDs of contributions being referenced.</param>
public sealed record AgentContribution(
    string AgentName,
    string Content,
    string? TargetAgent = null,
    IReadOnlyList<Guid>? ReferencedContributions = null)
{
    /// <summary>
    /// Gets a unique identifier for this contribution.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
}