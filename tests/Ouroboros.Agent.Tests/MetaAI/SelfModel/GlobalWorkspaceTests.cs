using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Tests.MetaAI.SelfModel;

[Trait("Category", "Unit")]
public class GlobalWorkspaceTests
{
    private readonly GlobalWorkspace _sut;

    public GlobalWorkspaceTests()
    {
        _sut = new GlobalWorkspace();
    }

    [Fact]
    public void AddItem_NullContent_ThrowsArgumentNullException()
    {
        // Act & Assert
        FluentActions.Invoking(() => _sut.AddItem(null!, WorkspacePriority.Normal, "source"))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddItem_NullSource_ThrowsArgumentNullException()
    {
        // Act & Assert
        FluentActions.Invoking(() => _sut.AddItem("content", WorkspacePriority.Normal, null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddItem_ValidInput_ReturnsItemWithCorrectProperties()
    {
        // Arrange
        string content = "Important finding";
        var tags = new List<string> { "analysis", "priority" };

        // Act
        WorkspaceItem item = _sut.AddItem(content, WorkspacePriority.Normal, "TestSource", tags);

        // Assert
        item.Should().NotBeNull();
        item.Content.Should().Be(content);
        item.Priority.Should().Be(WorkspacePriority.Normal);
        item.Source.Should().Be("TestSource");
        item.Tags.Should().BeEquivalentTo(tags);
        item.Id.Should().NotBeEmpty();
        item.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void AddItem_HighPriority_TriggersBroadcast()
    {
        // Act
        WorkspaceItem item = _sut.AddItem("urgent", WorkspacePriority.High, "source");

        // Assert
        List<WorkspaceBroadcast> broadcasts = _sut.GetRecentBroadcasts(10);
        broadcasts.Should().Contain(b => b.Item.Id == item.Id);
    }

    [Fact]
    public void GetItems_ReturnsNonExpiredItems()
    {
        // Arrange
        _sut.AddItem("active", WorkspacePriority.Normal, "source", lifetime: TimeSpan.FromHours(2));

        // Act
        List<WorkspaceItem> items = _sut.GetItems();

        // Assert
        items.Should().NotBeEmpty();
        items.Should().OnlyContain(i => i.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public void GetHighPriorityItems_ReturnsOnlyHighAndCritical()
    {
        // Arrange
        _sut.AddItem("low", WorkspacePriority.Low, "source");
        _sut.AddItem("normal", WorkspacePriority.Normal, "source");
        _sut.AddItem("high", WorkspacePriority.High, "source");
        _sut.AddItem("critical", WorkspacePriority.Critical, "source");

        // Act
        List<WorkspaceItem> result = _sut.GetHighPriorityItems();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(i => i.Priority >= WorkspacePriority.High);
    }

    [Fact]
    public void RemoveItem_ExistingItem_ReturnsTrue()
    {
        // Arrange
        WorkspaceItem item = _sut.AddItem("removable", WorkspacePriority.Normal, "source");

        // Act
        bool removed = _sut.RemoveItem(item.Id);

        // Assert
        removed.Should().BeTrue();
    }

    [Fact]
    public void RemoveItem_NonExistingItem_ReturnsFalse()
    {
        // Act
        bool removed = _sut.RemoveItem(Guid.NewGuid());

        // Assert
        removed.Should().BeFalse();
    }

    [Fact]
    public void BroadcastItem_NullItem_ThrowsArgumentNullException()
    {
        // Act & Assert
        FluentActions.Invoking(() => _sut.BroadcastItem(null!, "reason"))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BroadcastItem_NullReason_ThrowsArgumentNullException()
    {
        // Arrange
        var item = new WorkspaceItem(
            Guid.NewGuid(), "content", WorkspacePriority.Normal, "source",
            DateTime.UtcNow, DateTime.UtcNow.AddHours(1),
            new List<string>(), new Dictionary<string, object>());

        // Act & Assert
        FluentActions.Invoking(() => _sut.BroadcastItem(item, null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetRecentBroadcasts_ReturnsBroadcasts()
    {
        // Arrange
        var item = _sut.AddItem("broadcast me", WorkspacePriority.Normal, "source");
        _sut.BroadcastItem(item, "Manual broadcast");

        // Act
        List<WorkspaceBroadcast> broadcasts = _sut.GetRecentBroadcasts(10);

        // Assert
        broadcasts.Should().NotBeEmpty();
        broadcasts.Should().Contain(b => b.BroadcastReason == "Manual broadcast");
    }

    [Fact]
    public void SearchByTags_NullTags_ThrowsArgumentNullException()
    {
        // Act & Assert
        FluentActions.Invoking(() => _sut.SearchByTags(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SearchByTags_FindsItemsByTag()
    {
        // Arrange
        _sut.AddItem("alpha", WorkspacePriority.Normal, "source", new List<string> { "planning", "research" });
        _sut.AddItem("beta", WorkspacePriority.Normal, "source", new List<string> { "execution" });
        _sut.AddItem("gamma", WorkspacePriority.Normal, "source", new List<string> { "research", "data" });

        // Act
        List<WorkspaceItem> results = _sut.SearchByTags(new List<string> { "research" });

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(i => i.Tags.Contains("research"));
    }

    [Fact]
    public void GetStatistics_ReturnsStatistics()
    {
        // Arrange
        _sut.AddItem("low1", WorkspacePriority.Low, "SourceA");
        _sut.AddItem("high1", WorkspacePriority.High, "SourceB");
        _sut.AddItem("critical1", WorkspacePriority.Critical, "SourceA");

        // Act
        WorkspaceStatistics stats = _sut.GetStatistics();

        // Assert
        stats.TotalItems.Should().Be(3);
        stats.HighPriorityItems.Should().Be(2);
        stats.CriticalItems.Should().Be(1);
        stats.AverageAttentionWeight.Should().BeGreaterThan(0.0);
        stats.ItemsBySource.Should().ContainKey("SourceA");
        stats.ItemsBySource.Should().ContainKey("SourceB");
    }

    [Fact]
    public void DefaultConstructor_UsesDefaultPolicy()
    {
        // Arrange & Act
        var workspace = new GlobalWorkspace();

        // Assert - should not throw and should accept items
        var item = workspace.AddItem("test", WorkspacePriority.Normal, "source");
        item.Should().NotBeNull();
    }
}
