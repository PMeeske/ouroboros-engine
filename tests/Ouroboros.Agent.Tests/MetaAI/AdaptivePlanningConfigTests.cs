using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class AdaptivePlanningConfigTests
{
    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var sut = new AdaptivePlanningConfig();

        // Assert
        sut.MaxRetries.Should().Be(3);
        sut.EnableAutoReplan.Should().BeTrue();
        sut.FailureThreshold.Should().Be(0.5);
    }

    [Fact]
    public void Constructor_CustomValues_SetsProperties()
    {
        // Arrange & Act
        var sut = new AdaptivePlanningConfig(MaxRetries: 5, EnableAutoReplan: false, FailureThreshold: 0.8);

        // Assert
        sut.MaxRetries.Should().Be(5);
        sut.EnableAutoReplan.Should().BeFalse();
        sut.FailureThreshold.Should().Be(0.8);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var original = new AdaptivePlanningConfig();

        // Act
        var modified = original with { MaxRetries = 10 };

        // Assert
        modified.MaxRetries.Should().Be(10);
        modified.EnableAutoReplan.Should().Be(original.EnableAutoReplan);
        modified.FailureThreshold.Should().Be(original.FailureThreshold);
    }
}
