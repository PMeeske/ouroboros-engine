using Ouroboros.Core.Infrastructure.HealthCheck;

namespace Ouroboros.Tests.Database.HealthCheck;

/// <summary>
/// Tests for HealthStatus enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class HealthStatusTests
{
    [Fact]
    public void HealthStatus_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<HealthStatus>().Should().HaveCount(3);
        Enum.IsDefined(HealthStatus.Healthy).Should().BeTrue();
        Enum.IsDefined(HealthStatus.Degraded).Should().BeTrue();
        Enum.IsDefined(HealthStatus.Unhealthy).Should().BeTrue();
    }

    [Fact]
    public void HealthStatus_ValuesAreOrdered()
    {
        // Assert - Healthy should be best (0), Unhealthy worst (2)
        ((int)HealthStatus.Healthy).Should().BeLessThan((int)HealthStatus.Degraded);
        ((int)HealthStatus.Degraded).Should().BeLessThan((int)HealthStatus.Unhealthy);
    }
}