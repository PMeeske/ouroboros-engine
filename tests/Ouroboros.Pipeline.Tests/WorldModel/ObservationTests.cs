using FluentAssertions;
using Ouroboros.Pipeline.WorldModel;

namespace Ouroboros.Tests.WorldModel;

[Trait("Category", "Unit")]
public sealed class ObservationTests
{
    [Fact]
    public void Create_SetsValueAndConfidence()
    {
        // Act
        var obs = Observation.Create("hello", 0.9);

        // Assert
        obs.Value.Should().Be("hello");
        obs.Confidence.Should().Be(0.9);
        obs.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_ConfidenceAboveOne_ClampsToOne()
    {
        // Act
        var obs = Observation.Create("test", 1.5);

        // Assert
        obs.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void Create_ConfidenceBelowZero_ClampsToZero()
    {
        // Act
        var obs = Observation.Create("test", -0.5);

        // Assert
        obs.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void Create_NullValue_ThrowsArgumentNullException()
    {
        var act = () => Observation.Create(null!, 0.5);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Certain_SetsConfidenceToOne()
    {
        // Act
        var obs = Observation.Certain("definite value");

        // Assert
        obs.Value.Should().Be("definite value");
        obs.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void Certain_NullValue_ThrowsArgumentNullException()
    {
        var act = () => Observation.Certain(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetValueAs_CorrectType_ReturnsSome()
    {
        // Arrange
        var obs = Observation.Certain("text value");

        // Act
        var result = obs.GetValueAs<string>();

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value.Should().Be("text value");
    }

    [Fact]
    public void GetValueAs_WrongType_ReturnsNone()
    {
        // Arrange
        var obs = Observation.Certain("text value");

        // Act
        var result = obs.GetValueAs<int>();

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void GetValueAs_NumericType_ReturnsSome()
    {
        // Arrange
        var obs = Observation.Certain(42.0);

        // Act
        var result = obs.GetValueAs<double>();

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value.Should().Be(42.0);
    }
}
