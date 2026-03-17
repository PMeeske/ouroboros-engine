// <copyright file="WorkspaceStatisticsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Agent.Tests.MetaAI.SelfModel;

[Trait("Category", "Unit")]
public class WorkspaceStatisticsTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var totalItems = 50;
        var highPriority = 10;
        var critical = 3;
        var expired = 5;
        var avgWeight = 0.65;
        var itemsBySource = new Dictionary<string, int>
        {
            ["AnalysisEngine"] = 20,
            ["UserInput"] = 15,
            ["Background"] = 15
        };

        // Act
        var stats = new WorkspaceStatistics(
            totalItems, highPriority, critical, expired, avgWeight, itemsBySource);

        // Assert
        stats.TotalItems.Should().Be(totalItems);
        stats.HighPriorityItems.Should().Be(highPriority);
        stats.CriticalItems.Should().Be(critical);
        stats.ExpiredItems.Should().Be(expired);
        stats.AverageAttentionWeight.Should().Be(avgWeight);
        stats.ItemsBySource.Should().BeEquivalentTo(itemsBySource);
    }

    [Fact]
    public void Constructor_WithZeroValues_Succeeds()
    {
        var stats = new WorkspaceStatistics(0, 0, 0, 0, 0.0, new Dictionary<string, int>());

        stats.TotalItems.Should().Be(0);
        stats.HighPriorityItems.Should().Be(0);
        stats.CriticalItems.Should().Be(0);
        stats.ExpiredItems.Should().Be(0);
        stats.AverageAttentionWeight.Should().Be(0.0);
        stats.ItemsBySource.Should().BeEmpty();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var sources = new Dictionary<string, int> { ["a"] = 1 };

        var a = new WorkspaceStatistics(10, 5, 2, 1, 0.5, sources);
        var b = new WorkspaceStatistics(10, 5, 2, 1, 0.5, sources);

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentTotalItems_AreNotEqual()
    {
        var sources = new Dictionary<string, int>();

        var a = new WorkspaceStatistics(10, 0, 0, 0, 0.0, sources);
        var b = new WorkspaceStatistics(20, 0, 0, 0, 0.0, sources);

        a.Should().NotBe(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = new WorkspaceStatistics(10, 5, 2, 1, 0.5, new Dictionary<string, int>());

        var modified = original with { TotalItems = 20 };

        modified.TotalItems.Should().Be(20);
        modified.HighPriorityItems.Should().Be(original.HighPriorityItems);
    }
}
