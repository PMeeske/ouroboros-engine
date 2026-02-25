namespace Ouroboros.Providers;

/// <summary>
/// Result of an election with full transparency.
/// </summary>
public sealed record ElectionResult<T>(
    ResponseCandidate<T> Winner,
    IReadOnlyList<ResponseCandidate<T>> AllCandidates,
    ElectionStrategy Strategy,
    string Rationale,
    IReadOnlyDictionary<string, double> Votes);