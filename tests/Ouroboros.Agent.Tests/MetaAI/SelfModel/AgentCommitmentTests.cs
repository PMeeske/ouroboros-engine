using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;
using Xunit;

namespace Ouroboros.Tests.MetaAI.SelfModel;

[Trait("Category", "Unit")]
public class AgentCommitmentTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        string description = "Complete task analysis";
        var deadline = DateTime.UtcNow.AddDays(7);
        double priority = 0.8;
        var status = CommitmentStatus.InProgress;
        double progressPercent = 45.0;
        var dependencies = new List<string> { "dep1", "dep2" };
        var metadata = new Dictionary<string, object> { ["key"] = "value" };
        var createdAt = DateTime.UtcNow;
        DateTime? completedAt = null;

        // Act
        var sut = new AgentCommitment(
            id, description, deadline, priority, status,
            progressPercent, dependencies, metadata, createdAt, completedAt);

        // Assert
        sut.Id.Should().Be(id);
        sut.Description.Should().Be(description);
        sut.Deadline.Should().Be(deadline);
        sut.Priority.Should().Be(priority);
        sut.Status.Should().Be(status);
        sut.ProgressPercent.Should().Be(progressPercent);
        sut.Dependencies.Should().BeEquivalentTo(dependencies);
        sut.Metadata.Should().BeEquivalentTo(metadata);
        sut.CreatedAt.Should().Be(createdAt);
        sut.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var original = new AgentCommitment(
            Guid.NewGuid(),
            "Original task",
            DateTime.UtcNow.AddDays(7),
            0.5,
            CommitmentStatus.Planned,
            0.0,
            new List<string>(),
            new Dictionary<string, object>(),
            DateTime.UtcNow,
            null);

        // Act
        var completedAt = DateTime.UtcNow;
        var modified = original with
        {
            Status = CommitmentStatus.Completed,
            ProgressPercent = 100.0,
            CompletedAt = completedAt
        };

        // Assert
        modified.Status.Should().Be(CommitmentStatus.Completed);
        modified.ProgressPercent.Should().Be(100.0);
        modified.CompletedAt.Should().Be(completedAt);
        modified.Id.Should().Be(original.Id);
        modified.Description.Should().Be(original.Description);
    }
}
