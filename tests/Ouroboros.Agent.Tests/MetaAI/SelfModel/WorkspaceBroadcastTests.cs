// <copyright file="WorkspaceBroadcastTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Agent.Tests.MetaAI.SelfModel;

[Trait("Category", "Unit")]
public class WorkspaceBroadcastTests
{
    private static WorkspaceItem CreateItem() =>
        new(Guid.NewGuid(), "Content", WorkspacePriority.Normal, "Source",
            DateTime.UtcNow, DateTime.UtcNow.AddHours(1),
            new List<string>(), new Dictionary<string, object>());

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var item = CreateItem();
        var reason = "High priority item added";
        var broadcastTime = DateTime.UtcNow;

        // Act
        var broadcast = new WorkspaceBroadcast(item, reason, broadcastTime);

        // Assert
        broadcast.Item.Should().Be(item);
        broadcast.BroadcastReason.Should().Be(reason);
        broadcast.BroadcastTime.Should().Be(broadcastTime);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var item = CreateItem();
        var time = DateTime.UtcNow;

        var a = new WorkspaceBroadcast(item, "reason", time);
        var b = new WorkspaceBroadcast(item, "reason", time);

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentReason_AreNotEqual()
    {
        var item = CreateItem();
        var time = DateTime.UtcNow;

        var a = new WorkspaceBroadcast(item, "reason1", time);
        var b = new WorkspaceBroadcast(item, "reason2", time);

        a.Should().NotBe(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var broadcast = new WorkspaceBroadcast(CreateItem(), "original", DateTime.UtcNow);

        var modified = broadcast with { BroadcastReason = "updated" };

        modified.BroadcastReason.Should().Be("updated");
        modified.Item.Should().Be(broadcast.Item);
    }
}
