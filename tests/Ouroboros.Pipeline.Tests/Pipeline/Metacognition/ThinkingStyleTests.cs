namespace Ouroboros.Tests.Pipeline.Metacognition;

using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class ThinkingStyleTests
{
    [Fact]
    public void Balanced_HasEqualScores()
    {
        var style = ThinkingStyle.Balanced();

        style.AnalyticalScore.Should().Be(0.5);
        style.CreativeScore.Should().Be(0.5);
        style.SystematicScore.Should().Be(0.5);
        style.IntuitiveScore.Should().Be(0.5);
        style.BiasProfile.Should().BeEmpty();
    }

    [Fact]
    public void Analytical_FavorsAnalyticalDimension()
    {
        var style = ThinkingStyle.Analytical();

        style.AnalyticalScore.Should().BeGreaterThan(style.CreativeScore);
        style.DominantDimension.Should().Be("Analytical");
    }

    [Fact]
    public void Creative_FavorsCreativeDimension()
    {
        var style = ThinkingStyle.Creative();

        style.CreativeScore.Should().BeGreaterThan(style.AnalyticalScore);
        style.DominantDimension.Should().Be("Creative");
    }

    [Fact]
    public void HasSignificantBiases_ReturnsFalseWhenNoBiases()
    {
        ThinkingStyle.Balanced().HasSignificantBiases().Should().BeFalse();
    }

    [Fact]
    public void HasSignificantBiases_ReturnsTrueWhenBiasExceedsThreshold()
    {
        var style = ThinkingStyle.Balanced()
            .WithBias("confirmation", 0.7);

        style.HasSignificantBiases().Should().BeTrue();
    }

    [Fact]
    public void GetSignificantBiases_ReturnsFilteredAndSorted()
    {
        var style = ThinkingStyle.Balanced()
            .WithBias("confirmation", 0.7)
            .WithBias("anchoring", 0.4)
            .WithBias("minor", 0.1);

        var significant = style.GetSignificantBiases(0.3).ToList();

        significant.Should().HaveCount(2);
        significant[0].Bias.Should().Be("confirmation");
    }

    [Fact]
    public void WithBias_ClampsStrength()
    {
        var style = ThinkingStyle.Balanced().WithBias("test", 1.5);
        style.BiasProfile["test"].Should().Be(1.0);

        style = ThinkingStyle.Balanced().WithBias("test", -0.5);
        style.BiasProfile["test"].Should().Be(0.0);
    }
}
