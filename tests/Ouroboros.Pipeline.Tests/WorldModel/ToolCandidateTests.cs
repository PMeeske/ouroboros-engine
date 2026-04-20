using FluentAssertions;
using NSubstitute;
using Ouroboros.Pipeline.WorldModel;
using Ouroboros.Tools;

namespace Ouroboros.Tests.WorldModel;

[Trait("Category", "Unit")]
public sealed class ToolCandidateTests
{
    private static ITool CreateMockTool(string name = "test_tool")
    {
        var tool = Substitute.For<ITool>();
        tool.Name.Returns(name);
        tool.Description.Returns("A test tool");
        return tool;
    }

    [Fact]
    public void GetWeightedScore_CostStrategy_WeightsCostHighest()
    {
        // Arrange
        var tool = CreateMockTool();
        var candidate = new ToolCandidate(tool, 0.8, 0.2, 0.5, 0.9, []);

        // Act
        var score = candidate.GetWeightedScore(OptimizationStrategy.Cost);

        // Assert - Cost: (0.8 * 0.4) + ((1 - 0.2) * 0.5) + (0.9 * 0.1) = 0.32 + 0.4 + 0.09 = 0.81
        score.Should().BeApproximately(0.81, 0.01);
    }

    [Fact]
    public void GetWeightedScore_SpeedStrategy_WeightsSpeedHighest()
    {
        // Arrange
        var tool = CreateMockTool();
        var candidate = new ToolCandidate(tool, 0.5, 0.5, 1.0, 0.5, []);

        // Act
        var score = candidate.GetWeightedScore(OptimizationStrategy.Speed);

        // Assert - Speed: (0.5 * 0.3) + (1.0 * 0.5) + (0.5 * 0.2) = 0.15 + 0.5 + 0.1 = 0.75
        score.Should().BeApproximately(0.75, 0.01);
    }

    [Fact]
    public void GetWeightedScore_QualityStrategy_WeightsQualityHighest()
    {
        // Arrange
        var tool = CreateMockTool();
        var candidate = new ToolCandidate(tool, 0.5, 0.5, 0.5, 1.0, []);

        // Act
        var score = candidate.GetWeightedScore(OptimizationStrategy.Quality);

        // Assert - Quality: (0.5 * 0.3) + (1.0 * 0.6) + (0.5 * 0.1) = 0.15 + 0.6 + 0.05 = 0.8
        score.Should().BeApproximately(0.8, 0.01);
    }

    [Fact]
    public void GetWeightedScore_BalancedStrategy_BalancesAllFactors()
    {
        // Arrange
        var tool = CreateMockTool();
        var candidate = new ToolCandidate(tool, 0.5, 0.5, 0.5, 0.5, []);

        // Act
        var score = candidate.GetWeightedScore(OptimizationStrategy.Balanced);

        // Assert - Balanced: (0.5 * 0.4) + (0.5 * 0.3) + (0.5 * 0.2) + ((1-0.5) * 0.1) = 0.2 + 0.15 + 0.1 + 0.05 = 0.5
        score.Should().BeApproximately(0.5, 0.01);
    }

    [Fact]
    public void FromMatch_CreatesCandidate()
    {
        // Arrange
        var tool = CreateMockTool();
        var match = ToolMatch.Create("test_tool", 0.75, new[] { "search" });

        // Act
        var candidate = ToolCandidate.FromMatch(tool, match);

        // Assert
        candidate.Tool.Should().Be(tool);
        candidate.FitScore.Should().Be(0.75);
        candidate.CostScore.Should().Be(0.5); // default
        candidate.SpeedScore.Should().Be(0.5); // default
        candidate.QualityScore.Should().Be(0.75); // same as relevance
        candidate.MatchedCapabilities.Should().Contain("search");
    }

    [Fact]
    public void FromMatch_NullTool_ThrowsArgumentNullException()
    {
        var match = ToolMatch.Create("tool", 0.5);
        var act = () => ToolCandidate.FromMatch(null!, match);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromMatch_NullMatch_ThrowsArgumentNullException()
    {
        var tool = CreateMockTool();
        var act = () => ToolCandidate.FromMatch(tool, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(OptimizationStrategy.Cost)]
    [InlineData(OptimizationStrategy.Speed)]
    [InlineData(OptimizationStrategy.Quality)]
    [InlineData(OptimizationStrategy.Balanced)]
    public void GetWeightedScore_AllStrategies_ReturnsBetweenZeroAndOne(OptimizationStrategy strategy)
    {
        // Arrange
        var tool = CreateMockTool();
        var candidate = new ToolCandidate(tool, 0.5, 0.5, 0.5, 0.5, []);

        // Act
        var score = candidate.GetWeightedScore(strategy);

        // Assert
        score.Should().BeGreaterThanOrEqualTo(0.0);
        score.Should().BeLessThanOrEqualTo(1.0);
    }
}
