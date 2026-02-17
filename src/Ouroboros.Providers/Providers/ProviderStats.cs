namespace Ouroboros.Providers;

/// <summary>
/// Statistics for a provider in the round-robin pool.
/// </summary>
public sealed class ProviderStats
{
    public string Name { get; init; } = "";
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTime? LastSuccess { get; set; }
    public DateTime? LastFailure { get; set; }
    public bool IsHealthy => ConsecutiveFailures < 3;
    public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests : 1.0;
}