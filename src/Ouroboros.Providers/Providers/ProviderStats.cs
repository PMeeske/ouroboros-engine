namespace Ouroboros.Providers;

/// <summary>
/// Statistics for a provider in the round-robin pool.
/// Fields use <see cref="Interlocked"/> for thread-safe mutation.
/// </summary>
public sealed class ProviderStats
{
    public string Name { get; init; } = "";
    public int TotalRequests;
    public int SuccessfulRequests;
    public int FailedRequests;
    public int ConsecutiveFailures;
    public DateTime? LastSuccess { get; set; }
    public DateTime? LastFailure { get; set; }
    public bool IsHealthy => Volatile.Read(ref ConsecutiveFailures) < 3;
    public double SuccessRate => Volatile.Read(ref TotalRequests) > 0
        ? (double)Volatile.Read(ref SuccessfulRequests) / Volatile.Read(ref TotalRequests)
        : 1.0;
}
