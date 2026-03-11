using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Tests.MetaAI.SelfModel;

[Trait("Category", "Unit")]
public class WorkspaceStatisticsTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        int totalItems = 50;
        int highPriorityItems = 10;
        int criticalItems = 3;
        int expiredItems = 5;
        double averageAttentionWeight = 0.65;
        var itemsBySource = new Dictionary<string, int>
        {
            { "SourceA", 30 },
            { "SourceB", 20 }
        };

        // Act
        var sut = new WorkspaceStatistics(totalItems, highPriorityItems, criticalItems, expiredItems, averageAttentionWeight, itemsBySource);

        // Assert
        sut.TotalItems.Should().Be(totalItems);
        sut.HighPriorityItems.Should().Be(highPriorityItems);
        sut.CriticalItems.Should().Be(criticalItems);
        sut.ExpiredItems.Should().Be(expiredItems);
        sut.AverageAttentionWeight.Should().Be(averageAttentionWeight);
        sut.ItemsBySource.Should().BeEquivalentTo(itemsBySource);
    }
}
