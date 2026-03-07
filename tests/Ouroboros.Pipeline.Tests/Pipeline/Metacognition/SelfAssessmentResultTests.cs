namespace Ouroboros.Tests.Pipeline.Metacognition;

using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class SelfAssessmentResultTests
{
    private static DimensionScore CreateDimScore(
        PerformanceDimension dim,
        double score,
        double confidence,
        Trend trend = Trend.Stable)
    {
        return new DimensionScore(dim, score, confidence, ImmutableList<string>.Empty, trend);
    }

    [Fact]
    public void Empty_HasDefaultValues()
    {
        var result = SelfAssessmentResult.Empty();

        result.OverallScore.Should().Be(0.5);
        result.OverallConfidence.Should().Be(0.0);
        result.Strengths.Should().BeEmpty();
        result.Weaknesses.Should().BeEmpty();
        result.DimensionScores.Should().BeEmpty();
    }

    [Fact]
    public void FromDimensionScores_EmptyReturnsEmpty()
    {
        var result = SelfAssessmentResult.FromDimensionScores(
            ImmutableDictionary<PerformanceDimension, DimensionScore>.Empty);

        result.OverallScore.Should().Be(0.5);
        result.OverallConfidence.Should().Be(0.0);
    }

    [Fact]
    public void FromDimensionScores_ComputesWeightedScore()
    {
        var scores = ImmutableDictionary<PerformanceDimension, DimensionScore>.Empty
            .Add(PerformanceDimension.Accuracy,
                CreateDimScore(PerformanceDimension.Accuracy, 0.9, 0.8, Trend.Improving))
            .Add(PerformanceDimension.Consistency,
                CreateDimScore(PerformanceDimension.Consistency, 0.7, 0.6, Trend.Stable));

        var result = SelfAssessmentResult.FromDimensionScores(scores);

        result.OverallScore.Should().BeGreaterThan(0.0);
        result.OverallConfidence.Should().BeGreaterThan(0.0);
        result.DimensionScores.Should().HaveCount(2);
    }

    [Fact]
    public void FromDimensionScores_IdentifiesStrengths()
    {
        var scores = ImmutableDictionary<PerformanceDimension, DimensionScore>.Empty
            .Add(PerformanceDimension.Accuracy,
                CreateDimScore(PerformanceDimension.Accuracy, 0.9, 0.8, Trend.Improving));

        var result = SelfAssessmentResult.FromDimensionScores(scores);

        result.Strengths.Should().NotBeEmpty();
    }

    [Fact]
    public void FromDimensionScores_IdentifiesWeaknesses()
    {
        var scores = ImmutableDictionary<PerformanceDimension, DimensionScore>.Empty
            .Add(PerformanceDimension.Accuracy,
                CreateDimScore(PerformanceDimension.Accuracy, 0.3, 0.8, Trend.Declining));

        var result = SelfAssessmentResult.FromDimensionScores(scores);

        result.Weaknesses.Should().NotBeEmpty();
    }

    [Fact]
    public void GetDimensionScore_ReturnsSomeWhenExists()
    {
        var scores = ImmutableDictionary<PerformanceDimension, DimensionScore>.Empty
            .Add(PerformanceDimension.Accuracy,
                CreateDimScore(PerformanceDimension.Accuracy, 0.8, 0.7));

        var result = SelfAssessmentResult.FromDimensionScores(scores);
        var score = result.GetDimensionScore(PerformanceDimension.Accuracy);

        score.HasValue.Should().BeTrue();
    }

    [Fact]
    public void GetDimensionScore_ReturnsNoneWhenNotExists()
    {
        var result = SelfAssessmentResult.Empty();
        var score = result.GetDimensionScore(PerformanceDimension.Accuracy);

        score.HasValue.Should().BeFalse();
    }
}
