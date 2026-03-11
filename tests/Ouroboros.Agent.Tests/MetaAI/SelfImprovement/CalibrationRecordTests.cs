using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class CalibrationRecordTests
{
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange
        var recordedAt = DateTime.UtcNow;

        // Act
        var sut = new CalibrationRecord(0.85, true, recordedAt);

        // Assert
        sut.PredictedConfidence.Should().Be(0.85);
        sut.ActualSuccess.Should().BeTrue();
        sut.RecordedAt.Should().Be(recordedAt);
    }

    [Fact]
    public void Constructor_WithFailedOutcome_ShouldWork()
    {
        // Arrange & Act
        var sut = new CalibrationRecord(0.95, false, DateTime.UtcNow);

        // Assert
        sut.PredictedConfidence.Should().Be(0.95);
        sut.ActualSuccess.Should().BeFalse();
    }

    [Fact]
    public void RecordEquality_SameValues_ShouldBeEqual()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var a = new CalibrationRecord(0.7, true, now);
        var b = new CalibrationRecord(0.7, true, now);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var a = new CalibrationRecord(0.7, true, now);
        var b = new CalibrationRecord(0.7, false, now);

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new CalibrationRecord(0.5, true, DateTime.UtcNow);

        // Act
        var modified = original with { PredictedConfidence = 0.9 };

        // Assert
        modified.PredictedConfidence.Should().Be(0.9);
        modified.ActualSuccess.Should().BeTrue();
    }
}
