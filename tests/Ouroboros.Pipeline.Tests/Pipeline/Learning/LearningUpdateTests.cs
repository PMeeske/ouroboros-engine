namespace Ouroboros.Tests.Pipeline.Learning;

using Ouroboros.Pipeline.Learning;

[Trait("Category", "Unit")]
public class LearningUpdateTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var update = new LearningUpdate("param", 1.0, 0.8, -0.2, 0.9);

        update.ParameterName.Should().Be("param");
        update.OldValue.Should().Be(1.0);
        update.NewValue.Should().Be(0.8);
        update.Gradient.Should().Be(-0.2);
        update.Confidence.Should().Be(0.9);
    }

    [Fact]
    public void Magnitude_ReturnsAbsoluteDifference()
    {
        var update = new LearningUpdate("param", 1.0, 0.7, -0.3, 1.0);

        update.Magnitude.Should().BeApproximately(0.3, 0.001);
    }

    [Fact]
    public void FromGradient_ComputesNewValueViaGradientDescent()
    {
        // newValue = currentValue + (-learningRate * gradient)
        // newValue = 1.0 + (-0.1 * 2.0) = 1.0 - 0.2 = 0.8
        var update = LearningUpdate.FromGradient("param", 1.0, 2.0, 0.1);

        update.ParameterName.Should().Be("param");
        update.OldValue.Should().Be(1.0);
        update.NewValue.Should().BeApproximately(0.8, 0.001);
        update.Gradient.Should().Be(2.0);
        update.Confidence.Should().Be(1.0); // default
    }

    [Fact]
    public void FromGradient_ClampsConfidenceToZeroOne()
    {
        var update = LearningUpdate.FromGradient("param", 1.0, 0.5, 0.1, 2.0);
        update.Confidence.Should().Be(1.0);

        var update2 = LearningUpdate.FromGradient("param", 1.0, 0.5, 0.1, -1.0);
        update2.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void Scale_ScalesGradientAndDelta()
    {
        var update = new LearningUpdate("param", 1.0, 2.0, 0.5, 0.9);
        var scaled = update.Scale(0.5);

        // delta = 2.0 - 1.0 = 1.0, scaled = 0.5, newValue = 1.0 + 0.5 = 1.5
        scaled.NewValue.Should().BeApproximately(1.5, 0.001);
        scaled.Gradient.Should().BeApproximately(0.25, 0.001);
    }

    [Fact]
    public void MergeWith_CombinesUpdatesWeightedByConfidence()
    {
        var u1 = LearningUpdate.FromGradient("param", 1.0, 0.4, 0.1, 0.8);
        var u2 = LearningUpdate.FromGradient("param", 1.0, 0.6, 0.1, 0.2);

        var merged = u1.MergeWith(u2);

        merged.ParameterName.Should().Be("param");
        merged.Confidence.Should().Be(0.8); // max of both
    }

    [Fact]
    public void MergeWith_ThrowsForDifferentParameters()
    {
        var u1 = LearningUpdate.FromGradient("param1", 1.0, 0.5, 0.1);
        var u2 = LearningUpdate.FromGradient("param2", 1.0, 0.5, 0.1);

        var act = () => u1.MergeWith(u2);

        act.Should().Throw<ArgumentException>();
    }
}
