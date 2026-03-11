using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Tests.MetaAI.SelfModel;

[Trait("Category", "Unit")]
public class AgentIdentityStateTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        string name = "TestAgent";
        var capabilities = new List<AgentCapability>();
        var resources = new List<AgentResource>();
        var commitments = new List<AgentCommitment>();
        var performance = new AgentPerformance(
            0.9, 100.0, 10, 9, 1,
            new Dictionary<string, double>(),
            new Dictionary<string, double>(),
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow);
        var stateTimestamp = DateTime.UtcNow;
        var metadata = new Dictionary<string, object> { { "version", "1.0" } };

        // Act
        var sut = new AgentIdentityState(agentId, name, capabilities, resources, commitments, performance, stateTimestamp, metadata);

        // Assert
        sut.AgentId.Should().Be(agentId);
        sut.Name.Should().Be(name);
        sut.Capabilities.Should().BeSameAs(capabilities);
        sut.Resources.Should().BeSameAs(resources);
        sut.Commitments.Should().BeSameAs(commitments);
        sut.Performance.Should().Be(performance);
        sut.StateTimestamp.Should().Be(stateTimestamp);
        sut.Metadata.Should().BeEquivalentTo(metadata);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var performance = new AgentPerformance(
            0.9, 100.0, 10, 9, 1,
            new Dictionary<string, double>(),
            new Dictionary<string, double>(),
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow);
        var original = new AgentIdentityState(
            Guid.NewGuid(),
            "OriginalAgent",
            new List<AgentCapability>(),
            new List<AgentResource>(),
            new List<AgentCommitment>(),
            performance,
            DateTime.UtcNow,
            new Dictionary<string, object>());

        // Act
        var modified = original with { Name = "ModifiedAgent" };

        // Assert
        modified.Name.Should().Be("ModifiedAgent");
        modified.AgentId.Should().Be(original.AgentId);
        modified.Performance.Should().Be(original.Performance);
    }
}
