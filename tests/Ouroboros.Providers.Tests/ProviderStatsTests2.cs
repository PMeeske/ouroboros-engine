namespace Ouroboros.Tests.Providers;

[Trait("Category", "Unit")]
public sealed class ProviderStatsTests2
{
    [Fact]
    public void IsHealthy_NoFailures_ReturnsTrue()
    {
        var stats = new ProviderStats { Name = "Test" };
        stats.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void IsHealthy_ThreeConsecutiveFailures_ReturnsFalse()
    {
        var stats = new ProviderStats { Name = "Test", ConsecutiveFailures = 3 };
        stats.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public void IsHealthy_TwoConsecutiveFailures_ReturnsTrue()
    {
        var stats = new ProviderStats { Name = "Test", ConsecutiveFailures = 2 };
        stats.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void SuccessRate_NoRequests_Returns1()
    {
        var stats = new ProviderStats { Name = "Test" };
        stats.SuccessRate.Should().Be(1.0);
    }

    [Fact]
    public void SuccessRate_AllSuccessful_Returns1()
    {
        var stats = new ProviderStats
        {
            Name = "Test",
            TotalRequests = 10,
            SuccessfulRequests = 10
        };
        stats.SuccessRate.Should().Be(1.0);
    }

    [Fact]
    public void SuccessRate_HalfFailed_ReturnsHalf()
    {
        var stats = new ProviderStats
        {
            Name = "Test",
            TotalRequests = 10,
            SuccessfulRequests = 5
        };
        stats.SuccessRate.Should().BeApproximately(0.5, 0.01);
    }

    [Fact]
    public void LastSuccess_Null_WhenNoSuccesses()
    {
        var stats = new ProviderStats { Name = "Test" };
        stats.LastSuccess.Should().BeNull();
    }

    [Fact]
    public void LastSuccess_SetAndGet_RoundTrips()
    {
        var stats = new ProviderStats { Name = "Test" };
        var now = DateTime.UtcNow;
        stats.LastSuccess = now;

        stats.LastSuccess.Should().Be(now);
    }

    [Fact]
    public void LastFailure_Null_WhenNoFailures()
    {
        var stats = new ProviderStats { Name = "Test" };
        stats.LastFailure.Should().BeNull();
    }

    [Fact]
    public void LastFailure_SetAndGet_RoundTrips()
    {
        var stats = new ProviderStats { Name = "Test" };
        var now = DateTime.UtcNow;
        stats.LastFailure = now;

        stats.LastFailure.Should().Be(now);
    }

    [Fact]
    public void LastSuccess_SetToNull_ClearsValue()
    {
        var stats = new ProviderStats { Name = "Test" };
        stats.LastSuccess = DateTime.UtcNow;
        stats.LastSuccess = null;

        stats.LastSuccess.Should().BeNull();
    }

    [Fact]
    public void ConsecutiveFailures_ThreadSafe_IncrementAndRead()
    {
        var stats = new ProviderStats { Name = "Test" };
        Interlocked.Increment(ref stats.ConsecutiveFailures);
        Interlocked.Increment(ref stats.ConsecutiveFailures);

        Volatile.Read(ref stats.ConsecutiveFailures).Should().Be(2);
    }
}
