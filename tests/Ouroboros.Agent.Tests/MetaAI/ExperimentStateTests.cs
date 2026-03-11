using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class ExperimentStateTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var sut = new ExperimentState("exp-001", now);

        // Assert
        sut.ExperimentId.Should().Be("exp-001");
        sut.StartedAt.Should().Be(now);
    }

    [Fact]
    public void RecordEquality_SameValues_ShouldBeEqual()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var a = new ExperimentState("exp-1", now);
        var b = new ExperimentState("exp-1", now);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var a = new ExperimentState("exp-1", now);
        var b = new ExperimentState("exp-2", now);

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new ExperimentState("exp-1", DateTime.UtcNow);

        // Act
        var modified = original with { ExperimentId = "exp-2" };

        // Assert
        modified.ExperimentId.Should().Be("exp-2");
        modified.StartedAt.Should().Be(original.StartedAt);
    }
}
