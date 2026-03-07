namespace Ouroboros.Tests.Pipeline.Metacognition;

using Ouroboros.Pipeline.Metacognition;

[Trait("Category", "Unit")]
public class DimensionScoreTests
{
    [Fact]
    public void Unknown_HasMaxEntropyDefaults()
    {
        var score = DimensionScore.Unknown(PerformanceDimension.Accuracy);

        score.Score.Should().Be(0.5);
        score.Confidence.Should().Be(0.0);
        score.Trend.Should().Be(Trend.Unknown);
        score.Evidence.Should().BeEmpty();
    }

    [Fact]
    public void Create_ClampsScoreAndConfidence()
    {
        var score = DimensionScore.Create(
            PerformanceDimension.Speed, 1.5, -0.5, new[] { "evidence" });

        score.Score.Should().Be(1.0);
        score.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void WithBayesianUpdate_UpdatesScoreAndAddsEvidence()
    {
        var score = DimensionScore.Create(
            PerformanceDimension.Accuracy, 0.5, 0.5, new[] { "initial" });

        var updated = score.WithBayesianUpdate(0.8, 0.5, "new observation");

        updated.Evidence.Should().HaveCount(2);
        updated.Confidence.Should().BeGreaterThanOrEqualTo(score.Confidence);
    }

    [Fact]
    public void Validate_ReturnsSuccess_ForValidScore()
    {
        var score = DimensionScore.Create(
            PerformanceDimension.Accuracy, 0.7, 0.5, Array.Empty<string>());

        score.Validate().IsSuccess.Should().BeTrue();
    }
}
