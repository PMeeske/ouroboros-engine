using FluentAssertions;
using Ouroboros.Pipeline.WorldModel;

namespace Ouroboros.Tests.WorldModel;

[Trait("Category", "Unit")]
public sealed class CausalEdgeTests
{
    [Fact]
    public void Create_WithoutCondition_SetsStrengthAndNoCondition()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        // Act
        var edge = CausalEdge.Create(sourceId, targetId, 0.8);

        // Assert
        edge.SourceId.Should().Be(sourceId);
        edge.TargetId.Should().Be(targetId);
        edge.Strength.Should().Be(0.8);
        edge.Condition.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Create_WithCondition_SetsCondition()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        // Act
        var edge = CausalEdge.Create(sourceId, targetId, 0.5, "when raining");

        // Assert
        edge.Condition.HasValue.Should().BeTrue();
    }

    [Fact]
    public void Create_StrengthAboveOne_ClampsToOne()
    {
        // Act
        var edge = CausalEdge.Create(Guid.NewGuid(), Guid.NewGuid(), 1.5);

        // Assert
        edge.Strength.Should().Be(1.0);
    }

    [Fact]
    public void Create_StrengthBelowZero_ClampsToZero()
    {
        // Act
        var edge = CausalEdge.Create(Guid.NewGuid(), Guid.NewGuid(), -0.5);

        // Assert
        edge.Strength.Should().Be(0.0);
    }

    [Fact]
    public void Create_WithCondition_StrengthClamped()
    {
        // Act
        var edge = CausalEdge.Create(Guid.NewGuid(), Guid.NewGuid(), 2.0, "condition");

        // Assert
        edge.Strength.Should().Be(1.0);
    }

    [Fact]
    public void Create_NullCondition_ThrowsArgumentNullException()
    {
        // Act
        var act = () => CausalEdge.Create(Guid.NewGuid(), Guid.NewGuid(), 0.5, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Deterministic_SetsStrengthToOne()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        // Act
        var edge = CausalEdge.Deterministic(sourceId, targetId);

        // Assert
        edge.Strength.Should().Be(1.0);
        edge.Condition.HasValue.Should().BeFalse();
        edge.SourceId.Should().Be(sourceId);
        edge.TargetId.Should().Be(targetId);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var src = Guid.NewGuid();
        var tgt = Guid.NewGuid();
        var edge1 = CausalEdge.Create(src, tgt, 0.5);
        var edge2 = CausalEdge.Create(src, tgt, 0.5);

        // Assert
        edge1.Should().Be(edge2);
    }
}
