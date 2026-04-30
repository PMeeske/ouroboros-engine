// <copyright file="QueueStatisticsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Agent.MetaAI.Affect;

namespace Ouroboros.Tests.MetaAI.Affect;

[Trait("Category", "Unit")]
public sealed class QueueStatisticsTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Act
        var stats = new QueueStatistics(
            TotalTasks: 10,
            PendingTasks: 3,
            InProgressTasks: 2,
            CompletedTasks: 4,
            FailedTasks: 1,
            AverageBasePriority: 0.6,
            AverageModulatedPriority: 0.75,
            HighestThreat: 0.9,
            HighestOpportunity: 0.8);

        // Assert
        stats.TotalTasks.Should().Be(10);
        stats.PendingTasks.Should().Be(3);
        stats.InProgressTasks.Should().Be(2);
        stats.CompletedTasks.Should().Be(4);
        stats.FailedTasks.Should().Be(1);
        stats.AverageBasePriority.Should().Be(0.6);
        stats.AverageModulatedPriority.Should().Be(0.75);
        stats.HighestThreat.Should().Be(0.9);
        stats.HighestOpportunity.Should().Be(0.8);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var a = new QueueStatistics(5, 2, 1, 1, 1, 0.5, 0.6, 0.7, 0.8);
        var b = new QueueStatistics(5, 2, 1, 1, 1, 0.5, 0.6, 0.7, 0.8);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var stats = new QueueStatistics(10, 5, 3, 1, 1, 0.5, 0.6, 0.7, 0.8);

        // Act
        var modified = stats with { CompletedTasks = 8, PendingTasks = 0 };

        // Assert
        modified.CompletedTasks.Should().Be(8);
        modified.PendingTasks.Should().Be(0);
        modified.TotalTasks.Should().Be(10);
    }
}
