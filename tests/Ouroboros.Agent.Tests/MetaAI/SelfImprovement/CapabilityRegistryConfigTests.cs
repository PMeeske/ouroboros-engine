using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class CapabilityRegistryConfigTests
{
    [Fact]
    public void DefaultConstructor_ShouldSetDefaults()
    {
        // Arrange & Act
        var sut = new CapabilityRegistryConfig();

        // Assert
        sut.MinSuccessRateThreshold.Should().Be(0.6);
        sut.MinUsageCountForReliability.Should().Be(5);
        sut.CapabilityExpirationTime.Should().Be(default(TimeSpan));
    }

    [Fact]
    public void Constructor_WithCustomValues_ShouldSetAllProperties()
    {
        // Arrange
        var expiration = TimeSpan.FromHours(24);

        // Act
        var sut = new CapabilityRegistryConfig(
            MinSuccessRateThreshold: 0.9,
            MinUsageCountForReliability: 10,
            CapabilityExpirationTime: expiration);

        // Assert
        sut.MinSuccessRateThreshold.Should().Be(0.9);
        sut.MinUsageCountForReliability.Should().Be(10);
        sut.CapabilityExpirationTime.Should().Be(expiration);
    }

    [Fact]
    public void RecordEquality_SameDefaults_ShouldBeEqual()
    {
        // Arrange
        var a = new CapabilityRegistryConfig();
        var b = new CapabilityRegistryConfig();

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new CapabilityRegistryConfig();

        // Act
        var modified = original with { MinSuccessRateThreshold = 0.8 };

        // Assert
        modified.MinSuccessRateThreshold.Should().Be(0.8);
        modified.MinUsageCountForReliability.Should().Be(5);
    }
}
