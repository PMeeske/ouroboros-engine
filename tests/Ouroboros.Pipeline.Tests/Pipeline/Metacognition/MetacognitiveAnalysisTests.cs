namespace Ouroboros.Tests.Pipeline.Metacognition;

using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class MetacognitiveAnalysisTests
{
    private static MetacognitiveAnalysis CreateAnalysis(double qualityScore)
    {
        var trace = ReasoningTrace.Start();
        var reflection = ReflectionResult.HighQuality(trace) with { QualityScore = qualityScore };
        var style = ThinkingStyle.Balanced();
        var improvements = ImmutableList.Create("improve1", "improve2", "improve3", "improve4");

        return new MetacognitiveAnalysis(trace, reflection, style, improvements, DateTime.UtcNow);
    }

    [Fact]
    public void QualitySummary_ReturnsExcellentForHighScore()
    {
        var analysis = CreateAnalysis(0.95);
        analysis.QualitySummary.Should().Contain("Excellent");
    }

    [Fact]
    public void QualitySummary_ReturnsGoodForModerateHighScore()
    {
        var analysis = CreateAnalysis(0.75);
        analysis.QualitySummary.Should().Contain("Good");
    }

    [Fact]
    public void QualitySummary_ReturnsPoorForLowScore()
    {
        var analysis = CreateAnalysis(0.35);
        analysis.QualitySummary.Should().Contain("Poor");
    }

    [Fact]
    public void IsAcceptable_ReturnsTrueAboveThreshold()
    {
        var analysis = CreateAnalysis(0.8);
        analysis.IsAcceptable.Should().BeTrue();
    }

    [Fact]
    public void IsAcceptable_ReturnsFalseBelowThreshold()
    {
        var analysis = CreateAnalysis(0.3);
        analysis.IsAcceptable.Should().BeFalse();
    }

    [Fact]
    public void PriorityImprovements_ReturnsTopThree()
    {
        var analysis = CreateAnalysis(0.5);
        analysis.PriorityImprovements.Count().Should().BeLessThanOrEqualTo(3);
    }
}
