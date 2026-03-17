using FluentAssertions;
using NSubstitute;
using Ouroboros.Pipeline.Planning;
using Ouroboros.Pipeline.WorldModel;
using Ouroboros.Tools;

namespace Ouroboros.Tests.WorldModel;

[Trait("Category", "Unit")]
public sealed class ToolCapabilityMatcherTests
{
    private static ToolRegistry CreateRegistryWithTools(params (string Name, string Description)[] tools)
    {
        var registry = new ToolRegistry();
        foreach (var (name, description) in tools)
        {
            var tool = Substitute.For<ITool>();
            tool.Name.Returns(name);
            tool.Description.Returns(description);
            registry.Register(tool);
        }
        return registry;
    }

    [Fact]
    public void Constructor_NullRegistry_ThrowsArgumentNullException()
    {
        var act = () => new ToolCapabilityMatcher(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #region MatchToolsForGoal

    [Fact]
    public void MatchToolsForGoal_NullGoal_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new ToolRegistry();
        var matcher = new ToolCapabilityMatcher(registry);

        // Act
        var act = () => matcher.MatchToolsForGoal(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MatchToolsForGoal_InvalidMinScore_ReturnsFailure()
    {
        // Arrange
        var registry = new ToolRegistry();
        var matcher = new ToolCapabilityMatcher(registry);
        var goal = Goal.Atomic("Test");

        // Act
        var result = matcher.MatchToolsForGoal(goal, minScore: 1.5);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MatchToolsForGoal_NoTools_ReturnsEmptySuccess()
    {
        // Arrange
        var registry = new ToolRegistry();
        var matcher = new ToolCapabilityMatcher(registry);
        var goal = Goal.Atomic("Summarize the document");

        // Act
        var result = matcher.MatchToolsForGoal(goal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public void MatchToolsForGoal_MatchingTool_ReturnsMatch()
    {
        // Arrange
        var registry = CreateRegistryWithTools(
            ("summarize", "Summarize text into a shorter form"));
        var matcher = new ToolCapabilityMatcher(registry);
        var goal = Goal.Atomic("Summarize the document");

        // Act
        var result = matcher.MatchToolsForGoal(goal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void MatchToolsForGoal_WithMinScore_FiltersBelowThreshold()
    {
        // Arrange
        var registry = CreateRegistryWithTools(
            ("unrelated_tool", "Does something completely different"));
        var matcher = new ToolCapabilityMatcher(registry);
        var goal = Goal.Atomic("Summarize the document");

        // Act
        var result = matcher.MatchToolsForGoal(goal, minScore: 0.5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public void MatchToolsForGoal_MultipleTools_RankedByRelevance()
    {
        // Arrange
        var registry = CreateRegistryWithTools(
            ("summarize", "Summarize text documents"),
            ("translate", "Translate text between languages"),
            ("calculate", "Perform mathematical calculations"));
        var matcher = new ToolCapabilityMatcher(registry);
        var goal = Goal.Atomic("Summarize the text");

        // Act
        var result = matcher.MatchToolsForGoal(goal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Results should be ordered by relevance score descending
        if (result.Value.Count > 1)
        {
            result.Value[0].RelevanceScore.Should().BeGreaterThanOrEqualTo(result.Value[1].RelevanceScore);
        }
    }

    #endregion

    #region MatchToolsForGoalDescription

    [Fact]
    public void MatchToolsForGoalDescription_NullDescription_ThrowsArgumentNullException()
    {
        var matcher = new ToolCapabilityMatcher(new ToolRegistry());
        var act = () => matcher.MatchToolsForGoalDescription(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MatchToolsForGoalDescription_EmptyDescription_ReturnsFailure()
    {
        var matcher = new ToolCapabilityMatcher(new ToolRegistry());
        var result = matcher.MatchToolsForGoalDescription("   ");
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MatchToolsForGoalDescription_InvalidMinScore_ReturnsFailure()
    {
        var matcher = new ToolCapabilityMatcher(new ToolRegistry());
        var result = matcher.MatchToolsForGoalDescription("test", minScore: -0.1);
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region ScoreToolRelevance

    [Fact]
    public void ScoreToolRelevance_NullTool_ThrowsArgumentNullException()
    {
        var act = () => ToolCapabilityMatcher.ScoreToolRelevance(null!, "test");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ScoreToolRelevance_NullDescription_ThrowsArgumentNullException()
    {
        var tool = Substitute.For<ITool>();
        tool.Name.Returns("tool");
        var act = () => ToolCapabilityMatcher.ScoreToolRelevance(tool, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ScoreToolRelevance_ExactNameMatch_GetsBoost()
    {
        // Arrange
        var tool = Substitute.For<ITool>();
        tool.Name.Returns("summarize");
        tool.Description.Returns("A tool for summarization");

        // Act
        var scoreWithNameMatch = ToolCapabilityMatcher.ScoreToolRelevance(tool, "Use summarize to process");
        var scoreWithoutMatch = ToolCapabilityMatcher.ScoreToolRelevance(tool, "Do something else entirely");

        // Assert
        scoreWithNameMatch.Should().BeGreaterThan(scoreWithoutMatch);
    }

    [Fact]
    public void ScoreToolRelevance_ReturnsBetweenZeroAndOne()
    {
        // Arrange
        var tool = Substitute.For<ITool>();
        tool.Name.Returns("tool");
        tool.Description.Returns("Some description");

        // Act
        var score = ToolCapabilityMatcher.ScoreToolRelevance(tool, "Any goal description");

        // Assert
        score.Should().BeGreaterThanOrEqualTo(0.0);
        score.Should().BeLessThanOrEqualTo(1.0);
    }

    #endregion

    #region GetRequiredCapabilities

    [Fact]
    public void GetRequiredCapabilities_ExtractsKeywords()
    {
        // Act
        var capabilities = ToolCapabilityMatcher.GetRequiredCapabilities("Summarize the large document efficiently");

        // Assert
        capabilities.Should().Contain("summarize");
        capabilities.Should().Contain("large");
        capabilities.Should().Contain("document");
        capabilities.Should().Contain("efficiently");
        // Stop words should be removed
        capabilities.Should().NotContain("the");
    }

    [Fact]
    public void GetRequiredCapabilities_EmptyString_ReturnsEmpty()
    {
        var capabilities = ToolCapabilityMatcher.GetRequiredCapabilities("");
        capabilities.Should().BeEmpty();
    }

    [Fact]
    public void GetRequiredCapabilities_NullString_ThrowsArgumentNullException()
    {
        var act = () => ToolCapabilityMatcher.GetRequiredCapabilities(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetRequiredCapabilities_OnlyStopWords_ReturnsEmpty()
    {
        var capabilities = ToolCapabilityMatcher.GetRequiredCapabilities("the a an is");
        capabilities.Should().BeEmpty();
    }

    [Fact]
    public void GetRequiredCapabilities_ShortTokens_Excluded()
    {
        // Tokens less than 2 chars should be excluded
        var capabilities = ToolCapabilityMatcher.GetRequiredCapabilities("I do x");
        capabilities.Should().NotContain("x");
        capabilities.Should().NotContain("i");
    }

    #endregion

    #region GetBestMatch

    [Fact]
    public void GetBestMatch_NullGoal_ThrowsArgumentNullException()
    {
        var matcher = new ToolCapabilityMatcher(new ToolRegistry());
        var act = () => matcher.GetBestMatch(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetBestMatch_NoTools_ReturnsNone()
    {
        var matcher = new ToolCapabilityMatcher(new ToolRegistry());
        var result = matcher.GetBestMatch(Goal.Atomic("Test"));
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void GetBestMatch_MatchingTool_ReturnsSome()
    {
        var registry = CreateRegistryWithTools(("search", "Search and find information"));
        var matcher = new ToolCapabilityMatcher(registry);
        var goal = Goal.Atomic("Search for information about AI");

        var result = matcher.GetBestMatch(goal, minScore: 0.0);

        result.HasValue.Should().BeTrue();
    }

    #endregion

    #region GetBestMatchForDescription

    [Fact]
    public void GetBestMatchForDescription_NullDescription_ThrowsArgumentNullException()
    {
        var matcher = new ToolCapabilityMatcher(new ToolRegistry());
        var act = () => matcher.GetBestMatchForDescription(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetBestMatchForDescription_NoTools_ReturnsNone()
    {
        var matcher = new ToolCapabilityMatcher(new ToolRegistry());
        var result = matcher.GetBestMatchForDescription("search for info");
        result.HasValue.Should().BeFalse();
    }

    #endregion

    #region CreateMatchingStep / CreateDescriptionMatchingStep

    [Fact]
    public async Task CreateMatchingStep_ReturnsStep()
    {
        // Arrange
        var registry = CreateRegistryWithTools(("tool", "A tool"));
        var matcher = new ToolCapabilityMatcher(registry);
        var step = matcher.CreateMatchingStep(0.0);
        var goal = Goal.Atomic("Use tool");

        // Act
        var result = await step(goal);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateDescriptionMatchingStep_ReturnsStep()
    {
        // Arrange
        var registry = CreateRegistryWithTools(("tool", "A tool"));
        var matcher = new ToolCapabilityMatcher(registry);
        var step = matcher.CreateDescriptionMatchingStep(0.0);

        // Act
        var result = await step("Use tool to do something");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion
}
