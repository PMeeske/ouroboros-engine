// <copyright file="PrioritizedTaskTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Agent.MetaAI.Affect;
using AffectTaskStatus = Ouroboros.Agent.MetaAI.Affect.TaskStatus;

namespace Ouroboros.Tests.MetaAI.Affect;

[Trait("Category", "Unit")]
public sealed class PrioritizedTaskTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var appraisal = new TaskAppraisal(0.8, 0.6, 0.9, 0.7, "high priority");
        var created = DateTime.UtcNow;
        var due = DateTime.UtcNow.AddHours(1);
        var metadata = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var task = new PrioritizedTask(
            id, "Test Task", "A test task", 0.5, 0.8,
            appraisal, created, due, AffectTaskStatus.Pending, metadata);

        // Assert
        task.Id.Should().Be(id);
        task.Name.Should().Be("Test Task");
        task.Description.Should().Be("A test task");
        task.BasePriority.Should().Be(0.5);
        task.ModulatedPriority.Should().Be(0.8);
        task.Appraisal.Should().Be(appraisal);
        task.CreatedAt.Should().Be(created);
        task.DueAt.Should().Be(due);
        task.Status.Should().Be(AffectTaskStatus.Pending);
        task.Metadata.Should().ContainKey("key");
    }

    [Fact]
    public void Constructor_AllowsNullDueAt()
    {
        // Arrange
        var appraisal = new TaskAppraisal(0.1, 0.2, 0.3, 0.4, "low");

        // Act
        var task = new PrioritizedTask(
            Guid.NewGuid(), "Task", "desc", 0.5, 0.5,
            appraisal, DateTime.UtcNow, null, AffectTaskStatus.InProgress,
            new Dictionary<string, object>());

        // Assert
        task.DueAt.Should().BeNull();
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var appraisal = new TaskAppraisal(0.5, 0.5, 0.5, 0.5, "neutral");
        var task = new PrioritizedTask(
            Guid.NewGuid(), "Task", "desc", 0.5, 0.5,
            appraisal, DateTime.UtcNow, null, AffectTaskStatus.Pending,
            new Dictionary<string, object>());

        // Act
        var modified = task with { Status = AffectTaskStatus.Completed, ModulatedPriority = 1.0 };

        // Assert
        modified.Status.Should().Be(AffectTaskStatus.Completed);
        modified.ModulatedPriority.Should().Be(1.0);
        modified.Name.Should().Be("Task");
    }
}
