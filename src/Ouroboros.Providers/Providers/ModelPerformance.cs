namespace Ouroboros.Providers;

/// <summary>
/// Performance tracking for a model in the election system.
/// </summary>
public sealed class ModelPerformance
{
    public string ModelName { get; init; } = "";
    public int TotalElections { get; set; }
    public int Wins { get; set; }
    public double AverageScore { get; set; }
    public TimeSpan AverageLatency { get; set; }
    public double AverageCost { get; set; }
    public DateTime LastUsed { get; set; }

    public double WinRate => TotalElections > 0 ? (double)Wins / TotalElections : 0;
    public double ReliabilityScore => WinRate * 0.6 + (1 - Math.Min(1, AverageLatency.TotalSeconds / 30)) * 0.4;
}