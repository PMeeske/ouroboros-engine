using FluentAssertions;
using Ouroboros.Pipeline.Metacognition;

namespace Ouroboros.Tests.Metacognition;

[Trait("Category", "Unit")]
public sealed class ThinkingStyleTests
{
    [Fact]
    public void Balanced_ReturnsEqualScoresAndEmptyBiasProfile()
    {
        // Act
        var style = ThinkingStyle.Balanced();

        // Assert
        style.StyleName.Should().Be("Balanced");
        style.AnalyticalScore.Should().Be(0.5);
        style.CreativeScore.Should().Be(0.5);
        style.SystematicScore.Should().Be(0.5);
        style.IntuitiveScore.Should().Be(0.5);
        style.BiasProfile.Should().BeEmpty();
    }

    [Fact]
    public void Analytical_ReturnsHighAnalyticalScore()
    {
        // Act
        var style = ThinkingStyle.Analytical();

        // Assert
        style.StyleName.Should().Be("Analytical");
        style.AnalyticalScore.Should().Be(0.85);
        style.CreativeScore.Should().Be(0.35);
        style.SystematicScore.Should().Be(0.75);
        style.IntuitiveScore.Should().Be(0.25);
        style.BiasProfile.Should().BeEmpty();
    }

    [Fact]
    public void Creative_ReturnsHighCreativeScore()
    {
        // Act
        var style = ThinkingStyle.Creative();

        // Assert
        style.StyleName.Should().Be("Creative");
        style.AnalyticalScore.Should().Be(0.4);
        style.CreativeScore.Should().Be(0.9);
        style.SystematicScore.Should().Be(0.3);
        style.IntuitiveScore.Should().Be(0.7);
        style.BiasProfile.Should().BeEmpty();
    }

    [Fact]
    public void DominantDimension_WithAnalyticalStyle_ReturnsAnalytical()
    {
        // Arrange
        var style = ThinkingStyle.Analytical();

        // Act
        var dominant = style.DominantDimension;

        // Assert
        dominant.Should().Be("Analytical");
    }

    [Fact]
    public void DominantDimension_WithCreativeStyle_ReturnsCreative()
    {
        // Arrange
        var style = ThinkingStyle.Creative();

        // Act
        var dominant = style.DominantDimension;

        // Assert
        dominant.Should().Be("Creative");
    }

    [Fact]
    public void HasSignificantBiases_WithNoBiases_ReturnsFalse()
    {
        // Arrange
        var style = ThinkingStyle.Balanced();

        // Act & Assert
        style.HasSignificantBiases().Should().BeFalse();
    }

    [Fact]
    public void HasSignificantBiases_WithBiasAboveThreshold_ReturnsTrue()
    {
        // Arrange
        var style = ThinkingStyle.Balanced().WithBias("Confirmation", 0.7);

        // Act & Assert
        style.HasSignificantBiases(0.5).Should().BeTrue();
    }

    [Fact]
    public void HasSignificantBiases_WithBiasBelowThreshold_ReturnsFalse()
    {
        // Arrange
        var style = ThinkingStyle.Balanced().WithBias("Confirmation", 0.3);

        // Act & Assert
        style.HasSignificantBiases(0.5).Should().BeFalse();
    }

    [Fact]
    public void GetSignificantBiases_WithMultipleBiases_ReturnsFilteredAndOrdered()
    {
        // Arrange
        var style = ThinkingStyle.Balanced()
            .WithBias("ConfirmationBias", 0.8)
            .WithBias("AnchoringBias", 0.5)
            .WithBias("MinorBias", 0.1);

        // Act
        var significant = style.GetSignificantBiases(0.3).ToList();

        // Assert
        significant.Should().HaveCount(2);
        significant[0].Bias.Should().Be("ConfirmationBias");
        significant[0].Strength.Should().Be(0.8);
        significant[1].Bias.Should().Be("AnchoringBias");
        significant[1].Strength.Should().Be(0.5);
    }

    [Fact]
    public void GetSignificantBiases_WithNoBiasesAboveThreshold_ReturnsEmpty()
    {
        // Arrange
        var style = ThinkingStyle.Balanced().WithBias("Minor", 0.1);

        // Act
        var significant = style.GetSignificantBiases(0.3).ToList();

        // Assert
        significant.Should().BeEmpty();
    }

    [Fact]
    public void WithBias_ClampsValueToValidRange()
    {
        // Arrange
        var style = ThinkingStyle.Balanced();

        // Act
        var withHighBias = style.WithBias("TooHigh", 1.5);
        var withLowBias = style.WithBias("TooLow", -0.5);

        // Assert
        withHighBias.BiasProfile["TooHigh"].Should().Be(1.0);
        withLowBias.BiasProfile["TooLow"].Should().Be(0.0);
    }

    [Fact]
    public void WithBias_UpdatesExistingBias()
    {
        // Arrange
        var style = ThinkingStyle.Balanced().WithBias("Confirmation", 0.5);

        // Act
        var updated = style.WithBias("Confirmation", 0.8);

        // Assert
        updated.BiasProfile["Confirmation"].Should().Be(0.8);
        updated.BiasProfile.Should().HaveCount(1);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var style1 = ThinkingStyle.Balanced();
        var style2 = ThinkingStyle.Balanced();

        // Act & Assert
        style1.Should().Be(style2);
    }
}
