// ==========================================================
// Global Workspace Theory — Conscious Access Report
// Plan 4: Human-readable tick explanation
// ==========================================================

using System.Globalization;

namespace Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

/// <summary>
/// Explains what happened during a single cognitive tick.
/// </summary>
public sealed class ConsciousAccessReport
{
    /// <summary>
    /// Tick number.
    /// </summary>
    public required long TickNumber { get; init; }

    /// <summary>
    /// UTC timestamp when the tick occurred.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Duration of the tick in milliseconds.
    /// </summary>
    public required double DurationMs { get; init; }

    /// <summary>
    /// Chunks that were admitted to the workspace this tick.
    /// </summary>
    public required IReadOnlyList<AdmittedChunkInfo> Admitted { get; init; }

    /// <summary>
    /// Chunks that were evicted from the workspace this tick.
    /// </summary>
    public required IReadOnlyList<EvictedChunkInfo> Evicted { get; init; }

    /// <summary>
    /// Number of broadcast receivers that were updated.
    /// </summary>
    public required int BroadcastReceiverCount { get; init; }

    /// <summary>
    /// Workspace entropy at the end of the tick.
    /// </summary>
    public required double Entropy { get; init; }

    /// <summary>
    /// Human-readable summary of the tick.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Returns a formatted multi-line report.
    /// </summary>
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Tick #{TickNumber} ({DurationMs:F0}ms) — {Timestamp:O}");

        foreach (AdmittedChunkInfo info in Admitted)
        {
            sb.AppendLine($"  Admitted: \"{info.Content}\" (salience: {info.Salience.ToString("F2", CultureInfo.InvariantCulture)}, source: {info.Source})");
        }

        foreach (EvictedChunkInfo info in Evicted)
        {
            sb.AppendLine($"  Evicted:  \"{info.Content}\" (salience: {info.Salience.ToString("F2", CultureInfo.InvariantCulture)})");
        }

        sb.AppendLine($"  Broadcast: {BroadcastReceiverCount} receivers updated");
        sb.AppendLine($"  Entropy: {Entropy.ToString("F2", CultureInfo.InvariantCulture)}");

        return sb.ToString();
    }
}

/// <summary>
/// Information about a chunk admitted to the workspace.
/// </summary>
public sealed record AdmittedChunkInfo(
    string Content,
    double Salience,
    string Source);

/// <summary>
/// Information about a chunk evicted from the workspace.
/// </summary>
public sealed record EvictedChunkInfo(
    string Content,
    double Salience);
