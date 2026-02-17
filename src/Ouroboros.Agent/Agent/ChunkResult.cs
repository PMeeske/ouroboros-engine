namespace Ouroboros.Agent;

/// <summary>
/// Result of a chunk execution in divide-and-conquer pattern.
/// </summary>
public sealed record ChunkResult(
    int ChunkIndex,
    string Input,
    string Output,
    TimeSpan ExecutionTime,
    bool Success,
    string? Error = null);