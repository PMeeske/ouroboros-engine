// <copyright file="WorkspaceItemTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Agent.Tests.MetaAI.SelfModel;

[Trait("Category", "Unit")]
public class WorkspaceItemTests
{
    private static WorkspaceItem CreateItem(
        WorkspacePriority priority = WorkspacePriority.Normal,
        DateTime? createdAt = null,
        DateTime? expiresAt = null)
    {
        return new WorkspaceItem(
            Guid.NewGuid(),
            "Test content",
            priority,
            "TestSource",
            createdAt ?? DateTime.UtcNow,
            expiresAt ?? DateTime.UtcNow.AddHours(24),
            new List<string> { "tag1" },
            new Dictionary<string, object>());
    }

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var content = "Important insight";
        var priority = WorkspacePriority.High;
        var source = "AnalysisEngine";
        var createdAt = DateTime.UtcNow;
        var expiresAt = DateTime.UtcNow.AddHours(12);
        var tags = new List<string> { "analysis", "priority" };
        var metadata = new Dictionary<string, object> { ["confidence"] = 0.9 };

        // Act
        var item = new WorkspaceItem(id, content, priority, source, createdAt, expiresAt, tags, metadata);

        // Assert
        item.Id.Should().Be(id);
        item.Content.Should().Be(content);
        item.Priority.Should().Be(priority);
        item.Source.Should().Be(source);
        item.CreatedAt.Should().Be(createdAt);
        item.ExpiresAt.Should().Be(expiresAt);
        item.Tags.Should().BeEquivalentTo(tags);
        item.Metadata.Should().BeEquivalentTo(metadata);
    }

    // ── GetAttentionWeight ─────────────────────────────────────────

    [Fact]
    public void GetAttentionWeight_CriticalPriority_HasHigherWeightThanLow()
    {
        var critical = CreateItem(WorkspacePriority.Critical);
        var low = CreateItem(WorkspacePriority.Low);

        critical.GetAttentionWeight().Should().BeGreaterThan(low.GetAttentionWeight());
    }

    [Fact]
    public void GetAttentionWeight_RecentItem_HasHigherWeight()
    {
        var recent = CreateItem(createdAt: DateTime.UtcNow);
        var old = CreateItem(createdAt: DateTime.UtcNow.AddDays(-2));

        recent.GetAttentionWeight().Should().BeGreaterThanOrEqualTo(old.GetAttentionWeight());
    }

    [Fact]
    public void GetAttentionWeight_ExpiringSoon_GetsUrgencyBoost()
    {
        var expiringSoon = CreateItem(expiresAt: DateTime.UtcNow.AddMinutes(30));
        var expiresLater = CreateItem(expiresAt: DateTime.UtcNow.AddDays(7));

        expiringSoon.GetAttentionWeight().Should().BeGreaterThan(expiresLater.GetAttentionWeight());
    }

    [Fact]
    public void GetAttentionWeight_ReturnsNonNegative()
    {
        var item = CreateItem(WorkspacePriority.Low, createdAt: DateTime.UtcNow.AddDays(-30));

        item.GetAttentionWeight().Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Fact]
    public void GetAttentionWeight_CriticalRecentUrgent_MaximizesWeight()
    {
        var item = CreateItem(
            WorkspacePriority.Critical,
            createdAt: DateTime.UtcNow,
            expiresAt: DateTime.UtcNow.AddMinutes(10));

        // Priority (3/3) * 0.5 + recency (~1.0) * 0.3 + urgency (1.0) * 0.2
        item.GetAttentionWeight().Should().BeGreaterThanOrEqualTo(0.9);
    }

    [Fact]
    public void GetAttentionWeight_LowPriorityOldNotUrgent_MinimizesWeight()
    {
        var item = CreateItem(
            WorkspacePriority.Low,
            createdAt: DateTime.UtcNow.AddDays(-30),
            expiresAt: DateTime.UtcNow.AddDays(30));

        // Priority (0/3) * 0.5 + recency (~0.0) * 0.3 + urgency (0.0) * 0.2
        item.GetAttentionWeight().Should().BeLessThan(0.1);
    }

    // ── Equality ───────────────────────────────────────────────────

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var id = Guid.NewGuid();
        var time = DateTime.UtcNow;
        var tags = new List<string>();
        var meta = new Dictionary<string, object>();

        var a = new WorkspaceItem(id, "content", WorkspacePriority.Normal, "source", time, time.AddHours(1), tags, meta);
        var b = new WorkspaceItem(id, "content", WorkspacePriority.Normal, "source", time, time.AddHours(1), tags, meta);

        a.Should().Be(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = CreateItem();

        var modified = original with { Priority = WorkspacePriority.Critical };

        modified.Priority.Should().Be(WorkspacePriority.Critical);
        modified.Content.Should().Be(original.Content);
    }
}
