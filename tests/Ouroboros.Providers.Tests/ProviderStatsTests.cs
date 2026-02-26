namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ProviderStatsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var stats = new ProviderStats { Name = "test" };

        stats.Name.Should().Be("test");
        stats.TotalRequests.Should().Be(0);
        stats.SuccessfulRequests.Should().Be(0);
        stats.FailedRequests.Should().Be(0);
        stats.ConsecutiveFailures.Should().Be(0);
        stats.LastSuccess.Should().BeNull();
        stats.LastFailure.Should().BeNull();
    }

    [Fact]
    public void IsHealthy_NoFailures_ReturnsTrue()
    {
        var stats = new ProviderStats { ConsecutiveFailures = 0 };

        stats.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void IsHealthy_TwoConsecutiveFailures_ReturnsTrue()
    {
        var stats = new ProviderStats { ConsecutiveFailures = 2 };

        stats.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void IsHealthy_ThreeConsecutiveFailures_ReturnsFalse()
    {
        var stats = new ProviderStats { ConsecutiveFailures = 3 };

        stats.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public void SuccessRate_NoRequests_ReturnsOne()
    {
        var stats = new ProviderStats();

        stats.SuccessRate.Should().Be(1.0);
    }

    [Fact]
    public void SuccessRate_AllSuccessful_ReturnsOne()
    {
        var stats = new ProviderStats
        {
            TotalRequests = 10,
            SuccessfulRequests = 10,
        };

        stats.SuccessRate.Should().Be(1.0);
    }

    [Fact]
    public void SuccessRate_HalfSuccessful_ReturnsHalf()
    {
        var stats = new ProviderStats
        {
            TotalRequests = 10,
            SuccessfulRequests = 5,
        };

        stats.SuccessRate.Should().Be(0.5);
    }

    [Fact]
    public void MutableProperties_CanBeSet()
    {
        var stats = new ProviderStats { Name = "test" };
        var now = DateTime.UtcNow;

        stats.TotalRequests = 100;
        stats.SuccessfulRequests = 90;
        stats.FailedRequests = 10;
        stats.ConsecutiveFailures = 1;
        stats.LastSuccess = now;
        stats.LastFailure = now.AddMinutes(-5);

        stats.TotalRequests.Should().Be(100);
        stats.SuccessfulRequests.Should().Be(90);
        stats.FailedRequests.Should().Be(10);
        stats.ConsecutiveFailures.Should().Be(1);
        stats.LastSuccess.Should().Be(now);
        stats.LastFailure.Should().Be(now.AddMinutes(-5));
    }
}
