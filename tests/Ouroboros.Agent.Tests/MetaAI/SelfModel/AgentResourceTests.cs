using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;
using Xunit;

namespace Ouroboros.Tests.MetaAI.SelfModel;

[Trait("Category", "Unit")]
public class AgentResourceTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        string name = "GPU";
        string type = "Compute";
        double available = 75.0;
        double total = 100.0;
        string unit = "percent";
        var lastUpdated = DateTime.UtcNow;
        var metadata = new Dictionary<string, object> { ["vendor"] = "NVIDIA" };

        // Act
        var sut = new AgentResource(name, type, available, total, unit, lastUpdated, metadata);

        // Assert
        sut.Name.Should().Be(name);
        sut.Type.Should().Be(type);
        sut.Available.Should().Be(available);
        sut.Total.Should().Be(total);
        sut.Unit.Should().Be(unit);
        sut.LastUpdated.Should().Be(lastUpdated);
        sut.Metadata.Should().BeEquivalentTo(metadata);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var original = new AgentResource(
            "Memory", "RAM", 8.0, 16.0, "GB",
            DateTime.UtcNow, new Dictionary<string, object>());

        // Act
        var modified = original with { Available = 4.0 };

        // Assert
        modified.Available.Should().Be(4.0);
        modified.Name.Should().Be(original.Name);
        modified.Total.Should().Be(original.Total);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var metadata = new Dictionary<string, object>();

        var resource1 = new AgentResource("CPU", "Compute", 50.0, 100.0, "percent", timestamp, metadata);
        var resource2 = new AgentResource("CPU", "Compute", 50.0, 100.0, "percent", timestamp, metadata);

        // Act & Assert
        resource1.Should().Be(resource2);
    }
}
