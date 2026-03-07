namespace Ouroboros.Tests.Pipeline.Branches;

using Ouroboros.Pipeline.Branches;

[Trait("Category", "Unit")]
public class ProjectionMetricsTests
{
    [Fact]
    public void Constructor_SetsRequiredProperties()
    {
        var metrics = new ProjectionMetrics
        {
            TotalEpochs = 5,
            TotalBranches = 3,
            TotalEvents = 100
        };

        metrics.TotalEpochs.Should().Be(5);
        metrics.TotalBranches.Should().Be(3);
        metrics.TotalEvents.Should().Be(100);
    }

    [Fact]
    public void AverageEventsPerBranch_WithBranches_ReturnsCorrectAverage()
    {
        var metrics = new ProjectionMetrics
        {
            TotalEpochs = 1,
            TotalBranches = 4,
            TotalEvents = 100
        };

        metrics.AverageEventsPerBranch.Should().Be(25.0);
    }

    [Fact]
    public void AverageEventsPerBranch_WithZeroBranches_ReturnsZero()
    {
        var metrics = new ProjectionMetrics
        {
            TotalEpochs = 1,
            TotalBranches = 0,
            TotalEvents = 0
        };

        metrics.AverageEventsPerBranch.Should().Be(0.0);
    }

    [Fact]
    public void LastEpochAt_DefaultsToNull()
    {
        var metrics = new ProjectionMetrics
        {
            TotalEpochs = 0,
            TotalBranches = 0,
            TotalEvents = 0
        };

        metrics.LastEpochAt.Should().BeNull();
    }

    [Fact]
    public void CustomMetrics_DefaultsToEmptyDictionary()
    {
        var metrics = new ProjectionMetrics
        {
            TotalEpochs = 0,
            TotalBranches = 0,
            TotalEvents = 0
        };

        metrics.CustomMetrics.Should().NotBeNull().And.BeEmpty();
    }
}
