using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Tests.MetaAI.SelfModel;

[Trait("Category", "Unit")]
public class AgentPerformanceTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        double overallSuccessRate = 0.95;
        double averageResponseTime = 150.0;
        int totalTasks = 100;
        int successfulTasks = 95;
        int failedTasks = 5;
        var capabilitySuccessRates = new Dictionary<string, double> { { "coding", 0.98 } };
        var resourceUtilization = new Dictionary<string, double> { { "GPU", 0.75 } };
        var periodStart = DateTime.UtcNow.AddDays(-7);
        var periodEnd = DateTime.UtcNow;

        // Act
        var sut = new AgentPerformance(
            overallSuccessRate, averageResponseTime, totalTasks, successfulTasks, failedTasks,
            capabilitySuccessRates, resourceUtilization, periodStart, periodEnd);

        // Assert
        sut.OverallSuccessRate.Should().Be(overallSuccessRate);
        sut.AverageResponseTime.Should().Be(averageResponseTime);
        sut.TotalTasks.Should().Be(totalTasks);
        sut.SuccessfulTasks.Should().Be(successfulTasks);
        sut.FailedTasks.Should().Be(failedTasks);
        sut.CapabilitySuccessRates.Should().BeEquivalentTo(capabilitySuccessRates);
        sut.ResourceUtilization.Should().BeEquivalentTo(resourceUtilization);
        sut.MeasurementPeriodStart.Should().Be(periodStart);
        sut.MeasurementPeriodEnd.Should().Be(periodEnd);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var capRates = new Dictionary<string, double>();
        var resUtil = new Dictionary<string, double>();
        var start = DateTime.UtcNow.AddDays(-1);
        var end = DateTime.UtcNow;

        var perf1 = new AgentPerformance(0.9, 100.0, 10, 9, 1, capRates, resUtil, start, end);
        var perf2 = new AgentPerformance(0.9, 100.0, 10, 9, 1, capRates, resUtil, start, end);

        // Act & Assert
        perf1.Should().Be(perf2);
    }
}
