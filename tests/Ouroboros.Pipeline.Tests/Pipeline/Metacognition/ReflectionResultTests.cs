namespace Ouroboros.Tests.Pipeline.Metacognition;

using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class ReflectionResultTests
{
    [Fact]
    public void HighQuality_HasHighScores()
    {
        var trace = ReasoningTrace.Start();
        var result = ReflectionResult.HighQuality(trace);

        result.QualityScore.Should().Be(0.9);
        result.LogicalSoundness.Should().Be(0.95);
        result.HasIssues.Should().BeFalse();
        result.MeetsQualityThreshold().Should().BeTrue();
    }

    [Fact]
    public void Invalid_HasZeroScores()
    {
        var trace = ReasoningTrace.Start();
        var result = ReflectionResult.Invalid(trace);

        result.QualityScore.Should().Be(0.0);
        result.HasIssues.Should().BeTrue();
        result.MeetsQualityThreshold().Should().BeFalse();
    }

    [Fact]
    public void WithFallacy_AddsFallacy()
    {
        var trace = ReasoningTrace.Start();
        var result = ReflectionResult.HighQuality(trace)
            .WithFallacy("Ad hominem");

        result.IdentifiedFallacies.Should().Contain("Ad hominem");
        result.HasIssues.Should().BeTrue();
    }

    [Fact]
    public void WithMissedConsideration_AddsConsideration()
    {
        var trace = ReasoningTrace.Start();
        var result = ReflectionResult.HighQuality(trace)
            .WithMissedConsideration("Edge case X");

        result.MissedConsiderations.Should().Contain("Edge case X");
    }

    [Fact]
    public void WithImprovement_AddsImprovement()
    {
        var trace = ReasoningTrace.Start();
        var result = ReflectionResult.HighQuality(trace)
            .WithImprovement("Consider alternative approach");

        result.Improvements.Should().Contain("Consider alternative approach");
    }

    [Fact]
    public void MeetsQualityThreshold_UsesCustomThreshold()
    {
        var trace = ReasoningTrace.Start();
        var result = ReflectionResult.HighQuality(trace);

        result.MeetsQualityThreshold(0.95).Should().BeFalse();
        result.MeetsQualityThreshold(0.9).Should().BeTrue();
    }
}
