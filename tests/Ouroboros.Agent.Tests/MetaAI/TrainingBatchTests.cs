using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class TrainingBatchTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange
        var experiences = new List<Experience>();
        var metrics = new Dictionary<string, double> { { "loss", 0.05 } };
        var createdAt = DateTime.UtcNow;

        // Act
        var sut = new TrainingBatch(experiences, metrics, createdAt);

        // Assert
        sut.Experiences.Should().BeSameAs(experiences);
        sut.Metrics.Should().ContainKey("loss");
        sut.Metrics["loss"].Should().Be(0.05);
        sut.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Constructor_WithEmptyCollections_ShouldWork()
    {
        // Arrange & Act
        var sut = new TrainingBatch(
            new List<Experience>(),
            new Dictionary<string, double>(),
            DateTime.UtcNow);

        // Assert
        sut.Experiences.Should().BeEmpty();
        sut.Metrics.Should().BeEmpty();
    }

    [Fact]
    public void RecordEquality_SameReferences_ShouldBeEqual()
    {
        // Arrange
        var experiences = new List<Experience>();
        var metrics = new Dictionary<string, double>();
        var now = DateTime.UtcNow;
        var a = new TrainingBatch(experiences, metrics, now);
        var b = new TrainingBatch(experiences, metrics, now);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new TrainingBatch(
            new List<Experience>(),
            new Dictionary<string, double>(),
            DateTime.UtcNow);
        var newTime = DateTime.UtcNow.AddHours(1);

        // Act
        var modified = original with { CreatedAt = newTime };

        // Assert
        modified.CreatedAt.Should().Be(newTime);
        modified.Experiences.Should().BeSameAs(original.Experiences);
    }
}
