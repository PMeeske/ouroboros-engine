using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class DistributedOrchestrationConfigTests
{
    [Fact]
    public void DefaultConstructor_ShouldSetDefaults()
    {
        // Arrange & Act
        var sut = new DistributedOrchestrationConfig();

        // Assert
        sut.MaxAgents.Should().Be(10);
        sut.HeartbeatTimeout.Should().Be(default(TimeSpan));
        sut.EnableLoadBalancing.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithCustomValues_ShouldSetAllProperties()
    {
        // Arrange & Act
        var timeout = TimeSpan.FromSeconds(30);
        var sut = new DistributedOrchestrationConfig(
            MaxAgents: 5,
            HeartbeatTimeout: timeout,
            EnableLoadBalancing: false);

        // Assert
        sut.MaxAgents.Should().Be(5);
        sut.HeartbeatTimeout.Should().Be(timeout);
        sut.EnableLoadBalancing.Should().BeFalse();
    }

    [Fact]
    public void RecordEquality_SameDefaults_ShouldBeEqual()
    {
        // Arrange
        var a = new DistributedOrchestrationConfig();
        var b = new DistributedOrchestrationConfig();

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new DistributedOrchestrationConfig();

        // Act
        var modified = original with { MaxAgents = 20 };

        // Assert
        modified.MaxAgents.Should().Be(20);
        modified.EnableLoadBalancing.Should().BeTrue();
    }
}
