namespace Ouroboros.Tests.Pipeline.WorldModel;

using Moq;
using Ouroboros.Pipeline.WorldModel;

[Trait("Category", "Unit")]
public class ToolCandidateTests
{
    [Fact]
    public void GetWeightedScore_Balanced_UsesAllFactors()
    {
        var tool = new Mock<ITool>();
        var candidate = new ToolCandidate(
            tool.Object, 0.8, 0.3, 0.7, 0.9, new List<string> { "cap1" });

        var score = candidate.GetWeightedScore(OptimizationStrategy.Balanced);

        // Balanced: (FitScore * 0.4) + (QualityScore * 0.3) + (SpeedScore * 0.2) + ((1 - CostScore) * 0.1)
        var expected = (0.8 * 0.4) + (0.9 * 0.3) + (0.7 * 0.2) + (0.7 * 0.1);
        score.Should().BeApproximately(expected, 0.001);
    }

    [Fact]
    public void GetWeightedScore_Cost_PrioritizesCost()
    {
        var tool = new Mock<ITool>();
        var candidate = new ToolCandidate(
            tool.Object, 0.8, 0.2, 0.7, 0.9, new List<string>());

        var costScore = candidate.GetWeightedScore(OptimizationStrategy.Cost);
        var qualityScore = candidate.GetWeightedScore(OptimizationStrategy.Quality);

        // Cost strategy weighs 1-CostScore at 0.5, Quality strategy weighs QualityScore at 0.6
        costScore.Should().BeGreaterThan(0);
        qualityScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetWeightedScore_Speed_PrioritizesSpeed()
    {
        var tool = new Mock<ITool>();
        var fastCandidate = new ToolCandidate(
            tool.Object, 0.5, 0.5, 1.0, 0.5, new List<string>());
        var slowCandidate = new ToolCandidate(
            tool.Object, 0.5, 0.5, 0.1, 0.5, new List<string>());

        var fastScore = fastCandidate.GetWeightedScore(OptimizationStrategy.Speed);
        var slowScore = slowCandidate.GetWeightedScore(OptimizationStrategy.Speed);

        fastScore.Should().BeGreaterThan(slowScore);
    }

    [Fact]
    public void FromMatch_SetsDefaultScores()
    {
        var tool = new Mock<ITool>();
        var match = new ToolMatch("tool1", 0.85, new List<string> { "cap1" });

        var candidate = ToolCandidate.FromMatch(tool.Object, match);

        candidate.FitScore.Should().Be(0.85);
        candidate.CostScore.Should().Be(0.5);
        candidate.SpeedScore.Should().Be(0.5);
        candidate.QualityScore.Should().Be(0.85);
        candidate.MatchedCapabilities.Should().Contain("cap1");
    }
}
