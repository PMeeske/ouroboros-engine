// <copyright file="AgentPerformanceTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Agent.Tests.MetaAI.SelfModel;

[Trait("Category", "Unit")]
public class AgentPerformanceTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var successRate = 0.85;
        var avgResponseTime = 150.0;
        var totalTasks = 100;
        var successfulTasks = 85;
        var failedTasks = 15;
        var capabilityRates = new Dictionary<string, double> { ["reasoning"] = 0.9, ["coding"] = 0.8 };
        var resourceUtilization = new Dictionary<string, double> { ["cpu"] = 0.6, ["memory"] = 0.45 };
        var periodStart = DateTime.UtcNow.AddDays(-7);
        var periodEnd = DateTime.UtcNow;

        // Act
        var performance = new AgentPerformance(
            successRate, avgResponseTime, totalTasks,
            successfulTasks, failedTasks,
            capabilityRates, resourceUtilization,
            periodStart, periodEnd);

        // Assert
        performance.OverallSuccessRate.Should().Be(successRate);
        performance.AverageResponseTime.Should().Be(avgResponseTime);
        performance.TotalTasks.Should().Be(totalTasks);
        performance.SuccessfulTasks.Should().Be(successfulTasks);
        performance.FailedTasks.Should().Be(failedTasks);
        performance.CapabilitySuccessRates.Should().BeEquivalentTo(capabilityRates);
        performance.ResourceUtilization.Should().BeEquivalentTo(resourceUtilization);
        performance.MeasurementPeriodStart.Should().Be(periodStart);
        performance.MeasurementPeriodEnd.Should().Be(periodEnd);
    }

    [Fact]
    public void Constructor_WithZeroTasks_Succeeds()
    {
        var performance = new AgentPerformance(
            0.0, 0.0, 0, 0, 0,
            new Dictionary<string, double>(),
            new Dictionary<string, double>(),
            DateTime.UtcNow, DateTime.UtcNow);

        performance.TotalTasks.Should().Be(0);
        performance.SuccessfulTasks.Should().Be(0);
        performance.FailedTasks.Should().Be(0);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var start = DateTime.UtcNow.AddDays(-1);
        var end = DateTime.UtcNow;
        var rates = new Dictionary<string, double>();
        var utilization = new Dictionary<string, double>();

        var a = new AgentPerformance(0.8, 100, 10, 8, 2, rates, utilization, start, end);
        var b = new AgentPerformance(0.8, 100, 10, 8, 2, rates, utilization, start, end);

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentSuccessRate_AreNotEqual()
    {
        var start = DateTime.UtcNow;
        var end = DateTime.UtcNow;
        var rates = new Dictionary<string, double>();
        var utilization = new Dictionary<string, double>();

        var a = new AgentPerformance(0.8, 100, 10, 8, 2, rates, utilization, start, end);
        var b = new AgentPerformance(0.9, 100, 10, 8, 2, rates, utilization, start, end);

        a.Should().NotBe(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = new AgentPerformance(
            0.8, 100, 10, 8, 2,
            new Dictionary<string, double>(),
            new Dictionary<string, double>(),
            DateTime.UtcNow, DateTime.UtcNow);

        var modified = original with { TotalTasks = 20, SuccessfulTasks = 18 };

        modified.TotalTasks.Should().Be(20);
        modified.SuccessfulTasks.Should().Be(18);
        modified.OverallSuccessRate.Should().Be(original.OverallSuccessRate);
    }

    [Fact]
    public void Constructor_SuccessfulPlusFailedCanDifferFromTotal()
    {
        // The record doesn't enforce this invariant, it's a data holder
        var performance = new AgentPerformance(
            0.5, 100, 10, 3, 3,
            new Dictionary<string, double>(),
            new Dictionary<string, double>(),
            DateTime.UtcNow, DateTime.UtcNow);

        performance.TotalTasks.Should().Be(10);
        (performance.SuccessfulTasks + performance.FailedTasks).Should().Be(6);
    }
}
