// <copyright file="GlobalWorkspaceTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Agent.Tests.MetaAI.SelfModel;

/// <summary>
/// Unit tests for the GlobalWorkspace attention-based working memory component.
/// </summary>
[Trait("Category", "Unit")]
public class GlobalWorkspaceTests
{
    private readonly GlobalWorkspace _sut;

    public GlobalWorkspaceTests()
    {
        _sut = new GlobalWorkspace();
    }

    [Fact]
    public void AddItem_ReturnsItemWithCorrectProperties()
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
    public void AddItem_HighPriority_AutoBroadcasts()
    {
        // Act
        WorkspaceItem item = _sut.AddItem("urgent", WorkspacePriority.High, "source");

        // Assert — adding a high-priority item should trigger an automatic broadcast
        List<WorkspaceBroadcast> broadcasts = _sut.GetRecentBroadcasts(10);
        broadcasts.Should().Contain(b => b.Item.Id == item.Id);
    }

    [Fact]
    public void AddItem_WithCustomLifetime_SetsExpirationCorrectly()
    {
        // Arrange
        TimeSpan customLifetime = TimeSpan.FromMinutes(5);

        // Act
        WorkspaceItem item = _sut.AddItem("short-lived", WorkspacePriority.Low, "source", lifetime: customLifetime);

        // Assert — expiration should be approximately createdAt + 5 minutes
        TimeSpan expectedDelta = item.ExpiresAt - item.CreatedAt;
        expectedDelta.TotalMinutes.Should().BeApproximately(5.0, 1.0);
    }

    [Fact]
    public void GetItems_FiltersByMinimumPriority()
    {
        // Arrange
        _sut.AddItem("low", WorkspacePriority.Low, "source");
        _sut.AddItem("normal", WorkspacePriority.Normal, "source");
        _sut.AddItem("high", WorkspacePriority.High, "source");
        _sut.AddItem("critical", WorkspacePriority.Critical, "source");

        // Act
        List<WorkspaceItem> highAndAbove = _sut.GetItems(WorkspacePriority.High);

        // Assert
        highAndAbove.Should().HaveCount(2);
        highAndAbove.Should().OnlyContain(i => i.Priority >= WorkspacePriority.High);
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
        _sut.GetItems().Should().NotContain(i => i.Id == item.Id);
    }

    [Fact]
    public void RemoveItem_NonExistentId_ReturnsFalse()
    {
        // Act
        bool removed = _sut.RemoveItem(Guid.NewGuid());

        // Assert
        removed.Should().BeFalse();
    }

    [Fact]
    public void BroadcastItem_RecordsBroadcastRetrievableViaGetRecentBroadcasts()
    {
        // Arrange
        WorkspaceItem item = _sut.AddItem("broadcast me", WorkspacePriority.Normal, "source");

        // Act
        _sut.BroadcastItem(item, "Manual broadcast for testing");

        // Assert
        List<WorkspaceBroadcast> broadcasts = _sut.GetRecentBroadcasts(10);
        broadcasts.Should().Contain(b =>
            b.Item.Id == item.Id &&
            b.BroadcastReason == "Manual broadcast for testing");
    }

    [Fact]
    public void SearchByTags_ReturnsMatchingItems()
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
    public void ApplyAttentionPolicies_EnforcesMaxWorkspaceSize()
    {
        // Arrange — use a tiny workspace capacity
        var smallPolicy = new AttentionPolicy(
            MaxWorkspaceSize: 3,
            MaxHighPriorityItems: 10,
            DefaultItemLifetime: TimeSpan.FromHours(1),
            MinAttentionThreshold: 0.0);
        var workspace = new GlobalWorkspace(smallPolicy);

        // Add more items than capacity allows
        workspace.AddItem("item1", WorkspacePriority.Low, "source");
        workspace.AddItem("item2", WorkspacePriority.Low, "source");
        workspace.AddItem("item3", WorkspacePriority.Low, "source");
        workspace.AddItem("item4", WorkspacePriority.Low, "source");
        workspace.AddItem("item5", WorkspacePriority.Low, "source");

        // Act
        workspace.ApplyAttentionPolicies();

        // Assert — workspace should be trimmed to the max size
        List<WorkspaceItem> items = workspace.GetItems();
        items.Count.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public void ApplyAttentionPolicies_ExceedsMaxHighPriority_BroadcastsWarning()
    {
        // Arrange — use a policy with a low high-priority limit
        var policy = new AttentionPolicy(
            MaxWorkspaceSize: 100,
            MaxHighPriorityItems: 2,
            DefaultItemLifetime: TimeSpan.FromHours(1),
            MinAttentionThreshold: 0.0);
        var workspace = new GlobalWorkspace(policy);

        // Add more high-priority items than allowed
        workspace.AddItem("h1", WorkspacePriority.High, "source");
        workspace.AddItem("h2", WorkspacePriority.High, "source");
        workspace.AddItem("h3", WorkspacePriority.High, "source");

        // Act
        workspace.ApplyAttentionPolicies();

        // Assert — should broadcast an attention overload warning
        List<WorkspaceBroadcast> broadcasts = workspace.GetRecentBroadcasts(20);
        broadcasts.Should().Contain(b => b.BroadcastReason == "Attention capacity warning");
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectCounts()
    {
        // Arrange
        _sut.AddItem("low1", WorkspacePriority.Low, "SourceA");
        _sut.AddItem("high1", WorkspacePriority.High, "SourceB");
        _sut.AddItem("critical1", WorkspacePriority.Critical, "SourceA");

        // Act
        WorkspaceStatistics stats = _sut.GetStatistics();

        // Assert
        stats.TotalItems.Should().Be(3);
        stats.HighPriorityItems.Should().Be(2); // High + Critical
        stats.CriticalItems.Should().Be(1);
        stats.AverageAttentionWeight.Should().BeGreaterThan(0.0);
        stats.ItemsBySource.Should().ContainKey("SourceA");
        stats.ItemsBySource.Should().ContainKey("SourceB");
        stats.ItemsBySource["SourceA"].Should().Be(2);
        stats.ItemsBySource["SourceB"].Should().Be(1);
    }
}
