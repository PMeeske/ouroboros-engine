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
    private long _lastSuccessTicks;
    private long _lastFailureTicks;

    public DateTime? LastSuccess
    {
        get
        {
            long ticks = Interlocked.Read(ref _lastSuccessTicks);
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
        }
        set
        {
            Interlocked.Exchange(ref _lastSuccessTicks, value?.Ticks ?? 0);
        }
    }

    public DateTime? LastFailure
    {
        get
        {
            long ticks = Interlocked.Read(ref _lastFailureTicks);
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
        }
        set
        {
            Interlocked.Exchange(ref _lastFailureTicks, value?.Ticks ?? 0);
        }
    }
    public bool IsHealthy => Volatile.Read(ref ConsecutiveFailures) < 3;
    public double SuccessRate => Volatile.Read(ref TotalRequests) > 0
        ? (double)Volatile.Read(ref SuccessfulRequests) / Volatile.Read(ref TotalRequests)
        : 1.0;
}
