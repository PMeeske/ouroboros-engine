// ==========================================================
// Global Workspace Theory — Conscious Access Report
// Plan 4: Report builder
// ==========================================================

namespace Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

/// <summary>
/// Builds <see cref="ConsciousAccessReport"/> instances from tick data.
/// </summary>
public sealed class ConsciousAccessReportBuilder
{
    private long _tickNumber;
    private DateTime _startTime;
    private DateTime? _endTime;
    private readonly List<AdmittedChunkInfo> _admitted = new();
    private readonly List<EvictedChunkInfo> _evicted = new();
    private int _broadcastReceiverCount;
    private double _entropy;

    /// <summary>
    /// Starts building a report for a new tick.
    /// </summary>
    /// <param name="tickNumber">Tick number</param>
    /// <param name="startTime">When the tick started</param>
    public ConsciousAccessReportBuilder BeginTick(long tickNumber, DateTime startTime)
    {
        _tickNumber = tickNumber;
        _startTime = startTime;
        _endTime = null;
        _admitted.Clear();
        _evicted.Clear();
        _broadcastReceiverCount = 0;
        _entropy = 0.0;
        return this;
    }

    /// <summary>
    /// Records that a chunk was admitted.
    /// </summary>
    public ConsciousAccessReportBuilder WithAdmitted(ScoredCandidate scored)
    {
        string content = scored.Candidate.Content?.ToString() ?? "(null)";
        _admitted.Add(new AdmittedChunkInfo(content, scored.Salience, scored.Candidate.SourceSubsystem));
        return this;
    }

    /// <summary>
    /// Records that a chunk was evicted.
    /// </summary>
    public ConsciousAccessReportBuilder WithEvicted(ScoredCandidate scored)
    {
        string content = scored.Candidate.Content?.ToString() ?? "(null)";
        _evicted.Add(new EvictedChunkInfo(content, scored.Salience));
        return this;
    }

    /// <summary>
    /// Records the number of broadcast receivers updated.
    /// </summary>
    public ConsciousAccessReportBuilder WithBroadcastReceiverCount(int count)
    {
        _broadcastReceiverCount = count;
        return this;
    }

    /// <summary>
    /// Records the workspace entropy at end of tick.
    /// </summary>
    public ConsciousAccessReportBuilder WithEntropy(double entropy)
    {
        _entropy = entropy;
        return this;
    }

    /// <summary>
    /// Finalizes the tick and builds the report.
    /// </summary>
    /// <param name="endTime">When the tick ended</param>
    public ConsciousAccessReport Build(DateTime endTime)
    {
        _endTime = endTime;
        double durationMs = (_endTime.Value - _startTime).TotalMilliseconds;

        string summary = BuildSummary();

        return new ConsciousAccessReport
        {
            TickNumber = _tickNumber,
            Timestamp = _startTime,
            DurationMs = durationMs,
            Admitted = _admitted.ToList(),
            Evicted = _evicted.ToList(),
            BroadcastReceiverCount = _broadcastReceiverCount,
            Entropy = _entropy,
            Summary = summary
        };
    }

    private string BuildSummary()
    {
        var parts = new List<string>();

        if (_admitted.Count > 0)
        {
            parts.Add($"admitted {_admitted.Count} chunk(s)");
        }

        if (_evicted.Count > 0)
        {
            parts.Add($"evicted {_evicted.Count} chunk(s)");
        }

        if (parts.Count == 0)
        {
            parts.Add("no changes");
        }

        return string.Join(", ", parts);
    }
}
