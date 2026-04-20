// <copyright file="AgentCommitmentTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Agent.Tests.MetaAI.SelfModel;

[Trait("Category", "Unit")]
public class AgentCommitmentTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var description = "Complete data analysis pipeline";
        var deadline = DateTime.UtcNow.AddHours(4);
        var priority = 0.8;
        var status = CommitmentStatus.InProgress;
        var progress = 45.0;
        var dependencies = new List<string> { "data-ingestion", "preprocessing" };
        var metadata = new Dictionary<string, object> { ["team"] = "analytics" };
        var createdAt = DateTime.UtcNow;
        DateTime? completedAt = null;

        // Act
        var commitment = new AgentCommitment(
            id, description, deadline, priority, status,
            progress, dependencies, metadata, createdAt, completedAt);

        // Assert
        commitment.Id.Should().Be(id);
        commitment.Description.Should().Be(description);
        commitment.Deadline.Should().Be(deadline);
        commitment.Priority.Should().Be(priority);
        commitment.Status.Should().Be(CommitmentStatus.InProgress);
        commitment.ProgressPercent.Should().Be(45.0);
        commitment.Dependencies.Should().BeEquivalentTo(dependencies);
        commitment.Metadata.Should().BeEquivalentTo(metadata);
        commitment.CreatedAt.Should().Be(createdAt);
        commitment.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithCompletedStatus_SetsCompletedAt()
    {
        var completedAt = DateTime.UtcNow;

        var commitment = new AgentCommitment(
            Guid.NewGuid(), "task", DateTime.UtcNow, 0.5,
            CommitmentStatus.Completed, 100.0,
            new List<string>(), new Dictionary<string, object>(),
            DateTime.UtcNow, completedAt);

        commitment.CompletedAt.Should().Be(completedAt);
        commitment.ProgressPercent.Should().Be(100.0);
    }

    [Fact]
    public void With_CanUpdateStatus()
    {
        var original = new AgentCommitment(
            Guid.NewGuid(), "task", DateTime.UtcNow, 0.5,
            CommitmentStatus.Planned, 0.0,
            new List<string>(), new Dictionary<string, object>(),
            DateTime.UtcNow, null);

        var updated = original with { Status = CommitmentStatus.InProgress, ProgressPercent = 25.0 };

        updated.Status.Should().Be(CommitmentStatus.InProgress);
        updated.ProgressPercent.Should().Be(25.0);
        updated.Description.Should().Be(original.Description);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var id = Guid.NewGuid();
        var deadline = DateTime.UtcNow;
        var createdAt = DateTime.UtcNow;
        var deps = new List<string>();
        var meta = new Dictionary<string, object>();

        var a = new AgentCommitment(id, "task", deadline, 0.5, CommitmentStatus.Planned, 0.0, deps, meta, createdAt, null);
        var b = new AgentCommitment(id, "task", deadline, 0.5, CommitmentStatus.Planned, 0.0, deps, meta, createdAt, null);

        a.Should().Be(b);
    }

    [Theory]
    [InlineData(CommitmentStatus.Planned)]
    [InlineData(CommitmentStatus.InProgress)]
    [InlineData(CommitmentStatus.Completed)]
    [InlineData(CommitmentStatus.Failed)]
    [InlineData(CommitmentStatus.Cancelled)]
    [InlineData(CommitmentStatus.AtRisk)]
    public void Constructor_AcceptsAllStatuses(CommitmentStatus status)
    {
        var commitment = new AgentCommitment(
            Guid.NewGuid(), "task", DateTime.UtcNow, 0.5,
            status, 0.0,
            new List<string>(), new Dictionary<string, object>(),
            DateTime.UtcNow, null);

        commitment.Status.Should().Be(status);
    }

    [Fact]
    public void Constructor_WithEmptyDependencies_Succeeds()
    {
        var commitment = new AgentCommitment(
            Guid.NewGuid(), "task", DateTime.UtcNow, 0.5,
            CommitmentStatus.Planned, 0.0,
            new List<string>(), new Dictionary<string, object>(),
            DateTime.UtcNow, null);

        commitment.Dependencies.Should().BeEmpty();
    }
}
