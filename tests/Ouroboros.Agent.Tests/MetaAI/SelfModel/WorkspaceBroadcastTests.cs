using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;
using Xunit;

namespace Ouroboros.Tests.MetaAI.SelfModel;

[Trait("Category", "Unit")]
public class WorkspaceBroadcastTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var item = new WorkspaceItem(
            Guid.NewGuid(),
            "content",
            WorkspacePriority.Normal,
            "source",
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1),
            new List<string>(),
            new Dictionary<string, object>());
        string reason = "High priority item added";
        var broadcastTime = DateTime.UtcNow;

        // Act
        var sut = new WorkspaceBroadcast(item, reason, broadcastTime);

        // Assert
        sut.Item.Should().Be(item);
        sut.BroadcastReason.Should().Be(reason);
        sut.BroadcastTime.Should().Be(broadcastTime);
    }
}
