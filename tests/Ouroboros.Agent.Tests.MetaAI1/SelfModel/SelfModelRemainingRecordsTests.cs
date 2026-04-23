using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Agent.Tests.SelfModel;

[Trait("Category", "Unit")]
public class SelfModelRemainingRecordsTests
{
    #region WorkspaceBroadcast

    [Fact]
    public void WorkspaceBroadcast_Creation_ShouldSetProperties()
    {
        var items = new List<WorkspaceItem>();
        var broadcast = new WorkspaceBroadcast(Guid.NewGuid(), items, DateTime.UtcNow);

        broadcast.Id.Should().NotBe(Guid.Empty);
        broadcast.Items.Should().BeEquivalentTo(items);
        broadcast.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region WorkspaceItem

    [Fact]
    public void WorkspaceItem_Creation_ShouldSetProperties()
    {
        var createdAt = DateTime.UtcNow;
        var expiresAt = createdAt.AddHours(1);
        var item = new WorkspaceItem(Guid.NewGuid(), "content", WorkspacePriority.Normal, "source", createdAt, expiresAt, null);

        item.Content.Should().Be("content");
        item.Priority.Should().Be(WorkspacePriority.Normal);
        item.Source.Should().Be("source");
        item.CreatedAt.Should().Be(createdAt);
        item.ExpiresAt.Should().Be(expiresAt);
        item.AttentionCount.Should().BeNull();
    }

    [Fact]
    public void WorkspaceItem_Creation_WithAttentionCount_ShouldSetProperties()
    {
        var item = new WorkspaceItem(Guid.NewGuid(), "content", WorkspacePriority.High, "source", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), 5);

        item.AttentionCount.Should().Be(5);
    }

    [Fact]
    public void WorkspaceItem_IsExpired_ExpiredItem_ShouldBeTrue()
    {
        var item = new WorkspaceItem(Guid.NewGuid(), "content", WorkspacePriority.Normal, "source", DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1), null);
        item.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void WorkspaceItem_IsExpired_NotExpiredItem_ShouldBeFalse()
    {
        var item = new WorkspaceItem(Guid.NewGuid(), "content", WorkspacePriority.Normal, "source", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null);
        item.IsExpired.Should().BeFalse();
    }

    #endregion

    #region WorkspacePriority

    [Theory]
    [InlineData(WorkspacePriority.Low)]
    [InlineData(WorkspacePriority.Normal)]
    [InlineData(WorkspacePriority.High)]
    [InlineData(WorkspacePriority.Critical)]
    public void WorkspacePriority_AllValues_ShouldBeDefined(WorkspacePriority priority)
    {
        ((int)priority).Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region WorkspaceStatistics

    [Fact]
    public void WorkspaceStatistics_Creation_ShouldSetProperties()
    {
        var stats = new WorkspaceStatistics(10, 2, 5, 3, TimeSpan.FromMinutes(30), DateTime.UtcNow);

        stats.TotalItems.Should().Be(10);
        stats.ExpiredItems.Should().Be(2);
        stats.NormalPriorityItems.Should().Be(5);
        stats.HighPriorityItems.Should().Be(3);
        stats.AverageItemLifetime.Should().Be(TimeSpan.FromMinutes(30));
        stats.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion
}
