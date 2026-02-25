namespace Ouroboros.Pipeline.GraphRAG.Models;

/// <summary>
/// Represents a step in a query plan.
/// </summary>
/// <param name="Order">The execution order of this step.</param>
/// <param name="StepType">The type of operation to perform.</param>
/// <param name="Query">The query or expression for this step.</param>
/// <param name="Dependencies">IDs of steps that must complete before this one.</param>
public sealed record QueryStep(
    int Order,
    QueryStepType StepType,
    string Query,
    IReadOnlyList<int> Dependencies)
{
    /// <summary>
    /// Gets the entity types to filter for this step.
    /// </summary>
    public IReadOnlyList<string>? EntityTypeFilter { get; init; }

    /// <summary>
    /// Gets the relationship types to traverse for this step.
    /// </summary>
    public IReadOnlyList<string>? RelationshipTypeFilter { get; init; }

    /// <summary>
    /// Gets the maximum number of hops for graph traversal.
    /// </summary>
    public int? MaxHops { get; init; }
}