namespace Ouroboros.Agent;

/// <summary>
/// Configuration for divide-and-conquer execution.
/// </summary>
public sealed record DivideAndConquerConfig(
    int MaxParallelism = 4,
    int ChunkSize = 500,
    bool MergeResults = true,
    string MergeSeparator = "\n\n---\n\n");