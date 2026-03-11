using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class HierarchicalPlanningConfigTests
{
    [Fact]
    public void DefaultConstructor_ShouldSetDefaults()
    {
        // Arrange & Act
        var sut = new HierarchicalPlanningConfig();

        // Assert
        sut.MaxDepth.Should().Be(3);
        sut.MinStepsForDecomposition.Should().Be(3);
        sut.ComplexityThreshold.Should().Be(0.7);
    }

    [Fact]
    public void Constructor_WithCustomValues_ShouldSetAllProperties()
    {
        // Arrange & Act
        var sut = new HierarchicalPlanningConfig(
            MaxDepth: 5,
            MinStepsForDecomposition: 10,
            ComplexityThreshold: 0.9);

        // Assert
        sut.MaxDepth.Should().Be(5);
        sut.MinStepsForDecomposition.Should().Be(10);
        sut.ComplexityThreshold.Should().Be(0.9);
    }

    [Fact]
    public void RecordEquality_SameDefaults_ShouldBeEqual()
    {
        // Arrange
        var a = new HierarchicalPlanningConfig();
        var b = new HierarchicalPlanningConfig();

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new HierarchicalPlanningConfig();

        // Act
        var modified = original with { MaxDepth = 7 };

        // Assert
        modified.MaxDepth.Should().Be(7);
        modified.MinStepsForDecomposition.Should().Be(3);
    }
}
