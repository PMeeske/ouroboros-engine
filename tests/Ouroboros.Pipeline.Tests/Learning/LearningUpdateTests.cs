using Ouroboros.Pipeline.Learning;

namespace Ouroboros.Tests.Learning;

public class LearningUpdateTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Act
        var update = new LearningUpdate("param1", 1.0, 1.5, 0.3, 0.9);

        // Assert
        update.ParameterName.Should().Be("param1");
        update.OldValue.Should().Be(1.0);
        update.NewValue.Should().Be(1.5);
        update.Gradient.Should().Be(0.3);
        update.Confidence.Should().Be(0.9);
    }

    [Fact]
    public void Magnitude_ReturnsAbsoluteDifference()
    {
        // Arrange
        var update = new LearningUpdate("p", 1.0, 1.5, 0.3, 0.9);

        // Assert
        update.Magnitude.Should().BeApproximately(0.5, 0.001);
    }

    [Fact]
    public void Magnitude_WithNegativeChange_ReturnsPositive()
    {
        // Arrange
        var update = new LearningUpdate("p", 1.5, 1.0, -0.3, 0.9);

        // Assert
        update.Magnitude.Should().BeApproximately(0.5, 0.001);
    }

    [Fact]
    public void FromGradient_ComputesNewValueViaGradientDescent()
    {
        // Act
        var update = LearningUpdate.FromGradient("param", 1.0, 0.5, 0.1);

        // Assert
        // newValue = 1.0 + (-0.1 * 0.5) = 1.0 - 0.05 = 0.95
        update.NewValue.Should().BeApproximately(0.95, 0.001);
        update.OldValue.Should().Be(1.0);
        update.Gradient.Should().Be(0.5);
        update.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void FromGradient_ClampsConfidence()
    {
        // Act
        var update = LearningUpdate.FromGradient("p", 1.0, 0.5, 0.1, confidence: 2.0);

        // Assert
        update.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void FromGradient_WithNegativeConfidence_ClampsToZero()
    {
        // Act
        var update = LearningUpdate.FromGradient("p", 1.0, 0.5, 0.1, confidence: -1.0);

        // Assert
        update.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void Scale_ScalesDelta()
    {
        // Arrange
        var update = new LearningUpdate("p", 1.0, 2.0, 0.5, 0.9);

        // Act
        var scaled = update.Scale(0.5);

        // Assert
        // scaledDelta = (2.0 - 1.0) * 0.5 = 0.5
        // newValue = 1.0 + 0.5 = 1.5
        scaled.NewValue.Should().BeApproximately(1.5, 0.001);
        scaled.Gradient.Should().BeApproximately(0.25, 0.001);
        scaled.OldValue.Should().Be(1.0);
    }

    [Fact]
    public void Scale_WithZero_ResetsToOldValue()
    {
        // Arrange
        var update = new LearningUpdate("p", 1.0, 2.0, 0.5, 0.9);

        // Act
        var scaled = update.Scale(0.0);

        // Assert
        scaled.NewValue.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void MergeWith_SameParameter_WeightedAverageByConfidence()
    {
        // Arrange
        var update1 = new LearningUpdate("p", 1.0, 2.0, 0.5, 0.8);
        var update2 = new LearningUpdate("p", 1.0, 3.0, 1.0, 0.2);

        // Act
        var merged = update1.MergeWith(update2);

        // Assert
        // w1 = 0.8/1.0 = 0.8, w2 = 0.2/1.0 = 0.2
        // newValue = 2.0*0.8 + 3.0*0.2 = 1.6 + 0.6 = 2.2
        merged.NewValue.Should().BeApproximately(2.2, 0.001);
        merged.Gradient.Should().BeApproximately(0.5 * 0.8 + 1.0 * 0.2, 0.001);
        merged.Confidence.Should().Be(0.8); // Max confidence
        merged.ParameterName.Should().Be("p");
    }

    [Fact]
    public void MergeWith_DifferentParameter_ThrowsArgumentException()
    {
        // Arrange
        var update1 = new LearningUpdate("p1", 1.0, 2.0, 0.5, 0.8);
        var update2 = new LearningUpdate("p2", 1.0, 3.0, 1.0, 0.2);

        // Act & Assert
        var act = () => update1.MergeWith(update2);
        act.Should().Throw<ArgumentException>();
    }
}
