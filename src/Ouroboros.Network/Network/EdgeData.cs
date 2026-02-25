namespace Ouroboros.Network;

/// <summary>
/// Internal serialization data for TransitionEdge.
/// </summary>
internal sealed record EdgeData(
    Guid Id,
    Guid[] InputIds,
    Guid OutputId,
    string OperationName,
    string OperationSpecJson,
    DateTimeOffset CreatedAt,
    double? Confidence,
    long? DurationMs,
    string Hash);