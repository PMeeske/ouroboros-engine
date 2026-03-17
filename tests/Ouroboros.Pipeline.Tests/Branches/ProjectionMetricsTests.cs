using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Pipeline.Branches;

[Trait("Category", "Unit")]
public sealed class ProjectionMetricsTests
{
    [Fact]
    public void Constructor_WithRequiredProperties_SetsValues()
    {
        // Act
        var metrics = new ProjectionMetrics
        {
            TotalEpochs = 10,
            TotalBranches = 5,
            TotalEvents = 100
        };

        // Assert
        metrics.TotalEpochs.Should().Be(10);
        metrics.TotalBranches.Should().Be(5);
        metrics.TotalEvents.Should().Be(100);
    }

    [Fact]
    public void LastEpochAt_DefaultsToNull()
    {
        // Act
        var metrics = new ProjectionMetrics
        {
            TotalEpochs = 0,
            TotalBranches = 0,
            TotalEvents = 0
        };

        // Assert
        metrics.LastEpochAt.Should().BeNull();
    }

    [Fact]
    public void LastEpochAt_CanBeSet()
    {
        // Arrange
        var time = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var metrics = new ProjectionMetrics
        {
            TotalEpochs = 1,
            TotalBranches = 1,
            TotalEvents = 5,
            LastEpochAt = time
        };

        // Assert
        metrics.LastEpochAt.Should().Be(time);
    }

    [Fact]
    public void AverageEventsPerBranch_WithBranches_ReturnsCorrectAverage()
    {
        // Arrange
        var metrics = new ProjectionMetrics
        {
            TotalEpochs = 2,
            TotalBranches = 4,
            TotalEvents = 100
        };

        // Act & Assert
        metrics.AverageEventsPerBranch.Should().BeApproximately(25.0, 0.001);
    }

    [Fact]
    public void AverageEventsPerBranch_WithZeroBranches_ReturnsZero()
    {
        // Arrange
        var metrics = new ProjectionMetrics
        {
            TotalEpochs = 0,
            TotalBranches = 0,
            TotalEvents = 0
        };

        // Act & Assert
        metrics.AverageEventsPerBranch.Should().Be(0.0);
    }

    [Fact]
    public void AverageEventsPerBranch_WithOneBranch_ReturnsTotalEvents()
    {
        // Arrange
        var metrics = new ProjectionMetrics
        {
            TotalEpochs = 1,
            TotalBranches = 1,
            TotalEvents = 50
        };

        // Act & Assert
        metrics.AverageEventsPerBranch.Should().BeApproximately(50.0, 0.001);
    }

    [Fact]
    public void CustomMetrics_DefaultsToEmptyDictionary()
    {
        // Act
        var metrics = new ProjectionMetrics
        {
            TotalEpochs = 0,
            TotalBranches = 0,
            TotalEvents = 0
        };

        // Assert
        metrics.CustomMetrics.Should().NotBeNull();
        metrics.CustomMetrics.Should().BeEmpty();
    }

    [Fact]
    public void CustomMetrics_CanBeSetToCustomDictionary()
    {
        // Act
        var custom = new Dictionary<string, object>
        {
            ["custom_key"] = "custom_value"
        };

        var metrics = new ProjectionMetrics
        {
            TotalEpochs = 1,
            TotalBranches = 1,
            TotalEvents = 1,
            CustomMetrics = custom
        };

        // Assert
        metrics.CustomMetrics.Should().HaveCount(1);
        metrics.CustomMetrics["custom_key"].Should().Be("custom_value");
    }
}
