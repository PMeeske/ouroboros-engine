using Ouroboros.Core.Infrastructure.HealthCheck;

namespace Ouroboros.Tests.Database.HealthCheck;

/// <summary>
/// Tests for HealthCheckResult static factory methods.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class HealthCheckResultTests
{
    [Fact]
    public void Healthy_CreatesHealthyResult()
    {
        // Arrange
        var componentName = "TestComponent";
        var responseTime = 50L;
        var details = new Dictionary<string, object> { { "key", "value" } };

        // Act
        var result = HealthCheckResult.Healthy(componentName, responseTime, details);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.ComponentName.Should().Be(componentName);
        result.ResponseTime.Should().Be(responseTime);
        result.Details.Should().ContainKey("key");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Degraded_CreatesDegradedResult()
    {
        // Arrange
        var componentName = "TestComponent";
        var responseTime = 2500L;
        var warning = "Slow response";

        // Act
        var result = HealthCheckResult.Degraded(componentName, responseTime, null, warning);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.ComponentName.Should().Be(componentName);
        result.ResponseTime.Should().Be(responseTime);
        result.Error.Should().Be(warning);
    }

    [Fact]
    public void Unhealthy_CreatesUnhealthyResult()
    {
        // Arrange
        var componentName = "TestComponent";
        var responseTime = 5000L;
        var error = "Connection refused";

        // Act
        var result = HealthCheckResult.Unhealthy(componentName, responseTime, error);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.ComponentName.Should().Be(componentName);
        result.ResponseTime.Should().Be(responseTime);
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Constructor_WithNullDetails_CreatesEmptyDictionary()
    {
        // Act
        var result = new HealthCheckResult("Test", HealthStatus.Healthy, 100, null);

        // Assert
        result.Details.Should().NotBeNull();
        result.Details.Should().BeEmpty();
    }

    [Theory]
    [InlineData(HealthStatus.Healthy)]
    [InlineData(HealthStatus.Degraded)]
    [InlineData(HealthStatus.Unhealthy)]
    public void Constructor_WithDifferentStatuses_SetsCorrectly(HealthStatus status)
    {
        // Act
        var result = new HealthCheckResult("Test", status, 100);

        // Assert
        result.Status.Should().Be(status);
    }
}