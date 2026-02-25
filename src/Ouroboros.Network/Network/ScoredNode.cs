namespace Ouroboros.Network;

/// <summary>
/// A node with its semantic search score.
/// </summary>
/// <param name="Node">The matching node.</param>
/// <param name="Score">Similarity score (0-1).</param>
public sealed record ScoredNode(MonadNode Node, float Score);