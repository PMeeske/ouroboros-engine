// ==========================================================
// Global Workspace Theory — Capacity-Limited Workspace
// Plan 2: GwtWorkspace (genuine GWT workspace)
// ==========================================================

namespace Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

/// <summary>
/// Genuine Global Workspace Theory workspace with capacity-limited storage.
/// Distinct from the older GlobalWorkspace in SelfModel.
/// </summary>
public sealed class GwtWorkspace
{
    private readonly ConcurrentDictionary<Guid, WorkspaceChunk> _chunks = new();
    private readonly object _lock = new();

    /// <summary>
    /// Maximum number of chunks the workspace can hold (Baars/Cowan: 4-7).
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Creates a new GWT workspace.
    /// </summary>
    /// <param name="capacity">Capacity limit; default is 5</param>
    public GwtWorkspace(int capacity = 5)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
    }

    /// <summary>
    /// Current chunks in the workspace, ordered by admission time (oldest first).
    /// </summary>
    public IReadOnlyList<WorkspaceChunk> Chunks =>
        _chunks.Values.OrderBy(c => c.AdmittedAt).ToList();

    /// <summary>
    /// Runs competition among candidates and replaces lowest-salience chunks when full.
    /// </summary>
    /// <param name="candidates">Candidates competing for workspace entry</param>
    /// <returns>Result containing admitted and evicted chunks</returns>
    public CompetitionResult CompeteAndReplace(IEnumerable<ScoredCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        lock (_lock)
        {
            List<ScoredCandidate> ordered = candidates
                .OrderByDescending(c => c.Salience)
                .ToList();

            var admitted = new List<WorkspaceChunk>();
            var evicted = new List<WorkspaceChunk>();

            foreach (ScoredCandidate scored in ordered)
            {
                // Skip if already in workspace
                if (_chunks.ContainsKey(scored.Candidate.Id))
                {
                    continue;
                }

                if (_chunks.Count < Capacity)
                {
                    // Room available — admit directly
                    var chunk = new WorkspaceChunk(scored.Candidate, DateTime.UtcNow, scored.Salience);
                    _chunks[scored.Candidate.Id] = chunk;
                    admitted.Add(chunk);
                }
                else
                {
                    // Full — evict lowest-salience chunk if candidate is stronger
                    WorkspaceChunk? lowest = _chunks.Values
                        .OrderBy(c => c.Salience)
                        .FirstOrDefault();

                    if (lowest is not null && scored.Salience > lowest.Salience)
                    {
                        _chunks.TryRemove(lowest.Candidate.Id, out _);
                        evicted.Add(lowest);

                        var chunk = new WorkspaceChunk(scored.Candidate, DateTime.UtcNow, scored.Salience);
                        _chunks[scored.Candidate.Id] = chunk;
                        admitted.Add(chunk);
                    }
                }
            }

            return new CompetitionResult(admitted, evicted);
        }
    }

    /// <summary>
    /// Removes a chunk from the workspace by candidate ID.
    /// </summary>
    /// <param name="candidateId">ID of the candidate to remove</param>
    /// <returns>True if removed, false if not found</returns>
    public bool RemoveChunk(Guid candidateId)
    {
        return _chunks.TryRemove(candidateId, out _);
    }

    /// <summary>
    /// Clears all chunks from the workspace.
    /// </summary>
    public void Clear()
    {
        _chunks.Clear();
    }

    /// <summary>
    /// Gets an immutable snapshot of the current workspace state.
    /// </summary>
    public WorkspaceSnapshot GetSnapshot()
    {
        return new WorkspaceSnapshot(Chunks, Capacity, DateTime.UtcNow);
    }
}

/// <summary>
/// Result of a competition-and-replacement cycle.
/// </summary>
public sealed record CompetitionResult(
    IReadOnlyList<WorkspaceChunk> Admitted,
    IReadOnlyList<WorkspaceChunk> Evicted);
