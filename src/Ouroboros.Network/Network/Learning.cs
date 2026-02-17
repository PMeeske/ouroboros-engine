namespace Ouroboros.Network;

/// <summary>
/// Represents a learning captured during the thinking process.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="Category">Category of learning (skill, pattern, insight, fact, etc.).</param>
/// <param name="Content">What was learned.</param>
/// <param name="Context">The context in which it was learned.</param>
/// <param name="Confidence">Confidence level 0-1.</param>
/// <param name="Epoch">The epoch when this was learned.</param>
/// <param name="Timestamp">When this was learned.</param>
public sealed record Learning(
    string Id,
    string Category,
    string Content,
    string Context,
    double Confidence,
    long Epoch,
    DateTimeOffset Timestamp);