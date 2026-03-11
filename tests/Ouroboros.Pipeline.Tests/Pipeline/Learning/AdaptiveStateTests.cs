namespace Ouroboros.Tests.Pipeline.Learning;

using Ouroboros.Pipeline.Learning;

[Trait("Category", "Unit")]
public class AdaptiveStateTests
{
    [Fact]
    public void Initial_ReturnsDefaultStrategy()
    {
        // Act
        var state = AdaptiveState.Initial();

        // Assert
        state.CurrentStrategy.Should().Be(LearningStrategy.Default);
    }

    [Fact]
    public void Initial_ReturnsZeroBaselinePerformance()
    {
        // Act
        var state = AdaptiveState.Initial();

        // Assert
        state.BaselinePerformance.Should().Be(0.0);
    }

    [Fact]
    public void Initial_ReturnsEmptyPreviousStrategies()
    {
        // Act
        var state = AdaptiveState.Initial();

        // Assert
        state.PreviousStrategies.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var strategy = LearningStrategy.Default;
        var previousStrategies = ImmutableStack<LearningStrategy>.Empty.Push(strategy);

        // Act
        var state = new AdaptiveState(strategy, 0.75, previousStrategies);

        // Assert
        state.CurrentStrategy.Should().Be(strategy);
        state.BaselinePerformance.Should().Be(0.75);
        state.PreviousStrategies.Should().BeSameAs(previousStrategies);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        // Arrange
        var original = AdaptiveState.Initial();

        // Act
        var modified = original with { BaselinePerformance = 0.85 };

        // Assert
        modified.BaselinePerformance.Should().Be(0.85);
        original.BaselinePerformance.Should().Be(0.0);
    }

    [Fact]
    public void Equality_IdenticalValues_AreEqual()
    {
        // Arrange
        var a = AdaptiveState.Initial();
        var b = AdaptiveState.Initial();

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentBaseline_AreNotEqual()
    {
        // Arrange
        var a = AdaptiveState.Initial();
        var b = a with { BaselinePerformance = 1.0 };

        // Act & Assert
        a.Should().NotBe(b);
    }
}
