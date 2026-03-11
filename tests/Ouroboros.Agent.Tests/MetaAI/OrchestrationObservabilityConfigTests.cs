using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class OrchestrationObservabilityConfigTests
{
    [Fact]
    public void DefaultConstructor_ShouldSetDefaults()
    {
        // Arrange & Act
        var sut = new OrchestrationObservabilityConfig();

        // Assert
        sut.EnableTracing.Should().BeTrue();
        sut.EnableMetrics.Should().BeTrue();
        sut.EnableDetailedTags.Should().BeFalse();
        sut.SamplingRate.Should().Be(1.0);
    }

    [Fact]
    public void Constructor_WithCustomValues_ShouldSetAllProperties()
    {
        // Arrange & Act
        var sut = new OrchestrationObservabilityConfig(
            EnableTracing: false,
            EnableMetrics: false,
            EnableDetailedTags: true,
            SamplingRate: 0.5);

        // Assert
        sut.EnableTracing.Should().BeFalse();
        sut.EnableMetrics.Should().BeFalse();
        sut.EnableDetailedTags.Should().BeTrue();
        sut.SamplingRate.Should().Be(0.5);
    }

    [Fact]
    public void RecordEquality_SameDefaults_ShouldBeEqual()
    {
        // Arrange
        var a = new OrchestrationObservabilityConfig();
        var b = new OrchestrationObservabilityConfig();

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new OrchestrationObservabilityConfig();

        // Act
        var modified = original with { SamplingRate = 0.1 };

        // Assert
        modified.SamplingRate.Should().Be(0.1);
        modified.EnableTracing.Should().BeTrue();
    }
}
