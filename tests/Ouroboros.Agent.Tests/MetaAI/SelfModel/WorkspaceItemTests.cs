using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Tests.MetaAI.SelfModel;

[Trait("Category", "Unit")]
public class WorkspaceItemTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        string content = "test content";
        var priority = WorkspacePriority.High;
        string source = "test-source";
        var createdAt = DateTime.UtcNow;
        var expiresAt = DateTime.UtcNow.AddHours(1);
        var tags = new List<string> { "tag1", "tag2" };
        var metadata = new Dictionary<string, object> { { "key", "value" } };

        // Act
        var sut = new WorkspaceItem(id, content, priority, source, createdAt, expiresAt, tags, metadata);

        // Assert
        sut.Id.Should().Be(id);
        sut.Content.Should().Be(content);
        sut.Priority.Should().Be(priority);
        sut.Source.Should().Be(source);
        sut.CreatedAt.Should().Be(createdAt);
        sut.ExpiresAt.Should().Be(expiresAt);
        sut.Tags.Should().BeEquivalentTo(tags);
        sut.Metadata.Should().BeEquivalentTo(metadata);
    }

    [Fact]
    public void GetAttentionWeight_ReturnsValueBetweenZeroAndOne()
    {
        // Arrange
        var sut = new WorkspaceItem(
            Guid.NewGuid(),
            "content",
            WorkspacePriority.Normal,
            "source",
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(2),
            new List<string>(),
            new Dictionary<string, object>());

        // Act
        double weight = sut.GetAttentionWeight();

        // Assert
        weight.Should().BeGreaterThanOrEqualTo(0.0);
        weight.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void GetAttentionWeight_HigherPriority_GivesHigherWeight()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var expiresAt = now.AddHours(2);

        var lowItem = new WorkspaceItem(
            Guid.NewGuid(), "low", WorkspacePriority.Low, "source",
            now, expiresAt, new List<string>(), new Dictionary<string, object>());

        var criticalItem = new WorkspaceItem(
            Guid.NewGuid(), "critical", WorkspacePriority.Critical, "source",
            now, expiresAt, new List<string>(), new Dictionary<string, object>());

        // Act
        double lowWeight = lowItem.GetAttentionWeight();
        double criticalWeight = criticalItem.GetAttentionWeight();

        // Assert
        criticalWeight.Should().BeGreaterThan(lowWeight);
    }

    [Fact]
    public void GetAttentionWeight_ItemExpiringSoon_HasUrgencyComponent()
    {
        // Arrange
        var now = DateTime.UtcNow;

        var soonExpiring = new WorkspaceItem(
            Guid.NewGuid(), "urgent", WorkspacePriority.Normal, "source",
            now, now.AddMinutes(30), new List<string>(), new Dictionary<string, object>());

        var laterExpiring = new WorkspaceItem(
            Guid.NewGuid(), "not urgent", WorkspacePriority.Normal, "source",
            now, now.AddHours(5), new List<string>(), new Dictionary<string, object>());

        // Act
        double soonWeight = soonExpiring.GetAttentionWeight();
        double laterWeight = laterExpiring.GetAttentionWeight();

        // Assert
        soonWeight.Should().BeGreaterThan(laterWeight);
    }
}
