// ==========================================================
// Temporal Self Model Implementation
// Temporal self-continuity tracking with snapshots and projections
// ==========================================================

namespace Ouroboros.Agent.MetaAI.SelfModel;

/// <summary>
/// Growth rate information for a tracked dimension.
/// </summary>
/// <param name="Dimension">Name of the capability, belief, or trait.</param>
/// <param name="Category">Which category: Capability, Belief, or PersonalityTrait.</param>
/// <param name="RatePerDay">Average change per day.</param>
/// <param name="CurrentValue">Most recent value.</param>
public sealed record GrowthRate(
    string Dimension,
    string Category,
    double RatePerDay,
    double CurrentValue);

/// <summary>
/// Tracks temporal self-continuity by capturing periodic snapshots
/// of the agent's capabilities, beliefs, and personality traits,
/// and projecting future development trajectories.
/// </summary>
public sealed class TemporalSelfModel : ITemporalSelfModel
{
    private const int MaxSnapshots = 100;

    private readonly List<SelfSnapshot> _snapshots = new();
    private readonly object _lock = new();

    /// <summary>
    /// Captures the current self-state as a new snapshot.
    /// </summary>
    /// <param name="capabilities">Current capability proficiency scores.</param>
    /// <param name="beliefs">Current belief confidence scores.</param>
    /// <param name="personalityTraits">Current personality trait intensities.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created snapshot.</returns>
    public Task<Result<SelfSnapshot, string>> CaptureCurrentSelfAsync(
        Dictionary<string, double> capabilities,
        Dictionary<string, double> beliefs,
        Dictionary<string, double> personalityTraits,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(beliefs);
        ArgumentNullException.ThrowIfNull(personalityTraits);

        lock (_lock)
        {
            // Enforce one snapshot per session (no duplicates within 1 minute)
            if (_snapshots.Count > 0 &&
                (DateTime.UtcNow - _snapshots[^1].Timestamp).TotalMinutes < 1)
            {
                return Task.FromResult(
                    Result<SelfSnapshot, string>.Failure(
                        "A snapshot was already captured less than one minute ago."));
            }

            var snapshot = new SelfSnapshot(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow,
                new Dictionary<string, double>(capabilities),
                beliefs.ToDictionary(kv => kv.Key, kv => kv.Value.ToString("F4")),
                new Dictionary<string, double>(personalityTraits));

            _snapshots.Add(snapshot);

            // Prune oldest snapshots beyond capacity
            while (_snapshots.Count > MaxSnapshots)
            {
                _snapshots.RemoveAt(0);
            }

            return Task.FromResult(Result<SelfSnapshot, string>.Success(snapshot));
        }
    }

    /// <summary>
    /// Returns the ordered trajectory of snapshots with per-dimension growth rates.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The self trajectory with growth rates.</returns>
    public Task<Result<SelfTrajectory, string>> GetSelfTrajectoryAsync(
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_snapshots.Count < 2)
            {
                return Task.FromResult(
                    Result<SelfTrajectory, string>.Failure(
                        "At least two snapshots are required to compute a trajectory."));
            }

            List<SelfSnapshot> ordered = _snapshots.OrderBy(s => s.Timestamp).ToList();
            List<GrowthRate> growthRates = CalculateGrowthRates(ordered);

            var growthRateDict = growthRates.ToDictionary(
                gr => $"{gr.Category}:{gr.Dimension}",
                gr => gr.RatePerDay);
            var emerging = growthRates.Where(gr => gr.RatePerDay > 0.01).Select(gr => gr.Dimension).ToList();
            var declining = growthRates.Where(gr => gr.RatePerDay < -0.01).Select(gr => gr.Dimension).ToList();

            var trajectory = new SelfTrajectory(
                ordered,
                growthRateDict,
                emerging,
                declining);

            return Task.FromResult(Result<SelfTrajectory, string>.Success(trajectory));
        }
    }

    /// <summary>
    /// Projects a future self-snapshot by linearly extrapolating current trends.
    /// </summary>
    /// <param name="horizon">How far into the future to project.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A projected future snapshot.</returns>
    public Task<Result<SelfSnapshot, string>> ProjectFutureSelfAsync(
        TimeSpan horizon,
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_snapshots.Count < 2)
            {
                return Task.FromResult(
                    Result<SelfSnapshot, string>.Failure(
                        "At least two snapshots are required for projection."));
            }

            List<SelfSnapshot> ordered = _snapshots.OrderBy(s => s.Timestamp).ToList();
            List<GrowthRate> rates = CalculateGrowthRates(ordered);
            double daysAhead = horizon.TotalDays;

            SelfSnapshot latest = ordered[^1];
            var projectedCapabilities = new Dictionary<string, double>(latest.Capabilities);
            var projectedBeliefs = latest.Beliefs.ToDictionary(
                kv => kv.Key,
                kv => double.TryParse(kv.Value, out double v) ? v : 0.0);
            var projectedTraits = new Dictionary<string, double>(latest.PersonalityTraits);

            foreach (GrowthRate rate in rates)
            {
                double projected = Math.Clamp(rate.CurrentValue + rate.RatePerDay * daysAhead, 0.0, 1.0);

                switch (rate.Category)
                {
                    case "Capability":
                        projectedCapabilities[rate.Dimension] = projected;
                        break;
                    case "Belief":
                        projectedBeliefs[rate.Dimension] = projected;
                        break;
                    case "PersonalityTrait":
                        projectedTraits[rate.Dimension] = projected;
                        break;
                }
            }

            var futureSnapshot = new SelfSnapshot(
                Guid.NewGuid().ToString(),
                DateTime.UtcNow + horizon,
                projectedCapabilities,
                projectedBeliefs.ToDictionary(kv => kv.Key, kv => kv.Value.ToString("F4")),
                projectedTraits);

            return Task.FromResult(Result<SelfSnapshot, string>.Success(futureSnapshot));
        }
    }

    /// <summary>
    /// Measures temporal coherence as the average cosine similarity between adjacent snapshots.
    /// </summary>
    /// <returns>Coherence score between 0.0 and 1.0.</returns>
    public double MeasureTemporalCoherence()
    {
        lock (_lock)
        {
            if (_snapshots.Count < 2)
                return 1.0;

            List<SelfSnapshot> ordered = _snapshots.OrderBy(s => s.Timestamp).ToList();
            double totalSimilarity = 0.0;
            int comparisons = 0;

            for (int i = 1; i < ordered.Count; i++)
            {
                double similarity = CosineSimilarity(
                    SnapshotToVector(ordered[i - 1]),
                    SnapshotToVector(ordered[i]));
                totalSimilarity += similarity;
                comparisons++;
            }

            return comparisons > 0 ? totalSimilarity / comparisons : 1.0;
        }
    }

    private static List<GrowthRate> CalculateGrowthRates(List<SelfSnapshot> ordered)
    {
        var rates = new List<GrowthRate>();
        SelfSnapshot first = ordered[0];
        SelfSnapshot last = ordered[^1];
        double totalDays = (last.Timestamp - first.Timestamp).TotalDays;

        if (totalDays <= 0)
            return rates;

        AddRates(rates, first.Capabilities, last.Capabilities, "Capability", totalDays);
        AddBeliefRates(rates, first.Beliefs, last.Beliefs, totalDays);
        AddRates(rates, first.PersonalityTraits, last.PersonalityTraits, "PersonalityTrait", totalDays);

        return rates;
    }

    private static void AddBeliefRates(
        List<GrowthRate> rates,
        Dictionary<string, string> firstValues,
        Dictionary<string, string> lastValues,
        double totalDays)
    {
        foreach (string key in lastValues.Keys)
        {
            double currentValue = double.TryParse(lastValues[key], out double cv) ? cv : 0.0;
            double startValue = firstValues.TryGetValue(key, out string? sv) && double.TryParse(sv, out double parsed) ? parsed : 0.0;
            double ratePerDay = (currentValue - startValue) / totalDays;

            rates.Add(new GrowthRate(key, "Belief", ratePerDay, currentValue));
        }
    }

    private static void AddRates(
        List<GrowthRate> rates,
        Dictionary<string, double> firstValues,
        Dictionary<string, double> lastValues,
        string category,
        double totalDays)
    {
        foreach (string key in lastValues.Keys)
        {
            double currentValue = lastValues[key];
            double startValue = firstValues.TryGetValue(key, out double sv) ? sv : 0.0;
            double ratePerDay = (currentValue - startValue) / totalDays;

            rates.Add(new GrowthRate(key, category, ratePerDay, currentValue));
        }
    }

    private static double[] SnapshotToVector(SelfSnapshot snapshot)
    {
        var values = new List<double>();
        values.AddRange(snapshot.Capabilities.OrderBy(kv => kv.Key).Select(kv => kv.Value));
        values.AddRange(snapshot.Beliefs.OrderBy(kv => kv.Key)
            .Select(kv => double.TryParse(kv.Value, out double v) ? v : 0.0));
        values.AddRange(snapshot.PersonalityTraits.OrderBy(kv => kv.Key).Select(kv => kv.Value));
        return values.ToArray();
    }

    private static double CosineSimilarity(double[] a, double[] b)
    {
        if (a.Length == 0 || b.Length == 0)
            return 1.0;

        int length = Math.Min(a.Length, b.Length);
        double dot = 0, magA = 0, magB = 0;

        for (int i = 0; i < length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        double denominator = Math.Sqrt(magA) * Math.Sqrt(magB);
        return denominator > 1e-9 ? dot / denominator : 1.0;
    }
}
