using FluentAssertions;
using NSubstitute;
using Ouroboros.Pipeline.Planning;
using Ouroboros.Pipeline.WorldModel;
using Ouroboros.Tools;

namespace Ouroboros.Tests.WorldModel;

[Trait("Category", "Unit")]
public sealed class SmartToolSelectorTests
{
    private static (SmartToolSelector Selector, ToolRegistry Registry) CreateSelector(
        params (string Name, string Description)[] tools)
    {
        var worldState = WorldState.Empty();
        var registry = new ToolRegistry();
        foreach (var (name, description) in tools)
        {
            var tool = Substitute.For<ITool>();
            tool.Name.Returns(name);
            tool.Description.Returns(description);
            registry.Register(tool);
        }
        var matcher = new ToolCapabilityMatcher(registry);
        var selector = new SmartToolSelector(worldState, registry, matcher);
        return (selector, registry);
    }

    #region Constructor

    [Fact]
    public void Constructor_NullWorldState_ThrowsArgumentNullException()
    {
        var act = () => new SmartToolSelector(null!, new ToolRegistry(), new ToolCapabilityMatcher(new ToolRegistry()));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullRegistry_ThrowsArgumentNullException()
    {
        var act = () => new SmartToolSelector(WorldState.Empty(), null!, new ToolCapabilityMatcher(new ToolRegistry()));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullMatcher_ThrowsArgumentNullException()
    {
        var act = () => new SmartToolSelector(WorldState.Empty(), new ToolRegistry(), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        var act = () => new SmartToolSelector(WorldState.Empty(), new ToolRegistry(), new ToolCapabilityMatcher(new ToolRegistry()), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Configuration

    [Fact]
    public void Configuration_ReturnsCurrentConfig()
    {
        var (selector, _) = CreateSelector();
        selector.Configuration.Should().Be(SelectionConfig.Default);
    }

    [Fact]
    public void WithWorldState_ReturnsNewSelector()
    {
        // Arrange
        var (selector, _) = CreateSelector();
        var newState = WorldState.Empty().WithObservation("key", "val");

        // Act
        var newSelector = selector.WithWorldState(newState);

        // Assert
        newSelector.Should().NotBeSameAs(selector);
    }

    [Fact]
    public void WithConfig_ReturnsNewSelector()
    {
        // Arrange
        var (selector, _) = CreateSelector();
        var newConfig = SelectionConfig.ForSpeed();

        // Act
        var newSelector = selector.WithConfig(newConfig);

        // Assert
        newSelector.Configuration.Should().Be(newConfig);
    }

    #endregion

    #region SelectForGoalAsync

    [Fact]
    public async Task SelectForGoalAsync_NullGoal_ThrowsArgumentNullException()
    {
        var (selector, _) = CreateSelector();
        var act = () => selector.SelectForGoalAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SelectForGoalAsync_NoMatchingTools_ReturnsFailedSelection()
    {
        // Arrange
        var (selector, _) = CreateSelector();
        var goal = Goal.Atomic("Summarize the document");

        // Act
        var result = await selector.SelectForGoalAsync(goal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HasTools.Should().BeFalse();
    }

    [Fact]
    public async Task SelectForGoalAsync_MatchingTools_ReturnsSelection()
    {
        // Arrange
        var (selector, _) = CreateSelector(
            ("summarize", "Summarize text documents"));
        var goal = Goal.Atomic("Summarize this text document");

        // Act
        var result = await selector.SelectForGoalAsync(goal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Selection may or may not have tools depending on confidence threshold
    }

    [Fact]
    public async Task SelectForGoalAsync_CancelledToken_ReturnsFailure()
    {
        // Arrange
        var (selector, _) = CreateSelector();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await selector.SelectForGoalAsync(Goal.Atomic("Test"), cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cancelled");
    }

    [Fact]
    public async Task SelectForGoalAsync_RespectsMaxToolsConfig()
    {
        // Arrange
        var (selector, _) = CreateSelector(
            ("tool1", "search text data"),
            ("tool2", "search and find data"),
            ("tool3", "search web data"),
            ("tool4", "search files data"),
            ("tool5", "search database data"));
        var limitedSelector = selector.WithConfig(new SelectionConfig(MaxTools: 2, MinConfidence: 0.0));
        var goal = Goal.Atomic("search data");

        // Act
        var result = await limitedSelector.SelectForGoalAsync(goal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SelectedTools.Count.Should().BeLessThanOrEqualTo(2);
    }

    #endregion

    #region ApplyConstraints

    [Fact]
    public void ApplyConstraints_NoConstraints_ReturnsAllCandidates()
    {
        // Arrange
        var tool = Substitute.For<ITool>();
        tool.Name.Returns("tool");
        var candidates = new List<ToolCandidate>
        {
            new(tool, 0.5, 0.5, 0.5, 0.5, new[] { "cap1" })
        };

        // Act
        var result = SmartToolSelector.ApplyConstraints(candidates, Array.Empty<Constraint>().ToList());

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void ApplyConstraints_ExcludeConstraint_RemovesMatchingTool()
    {
        // Arrange
        var tool1 = Substitute.For<ITool>();
        tool1.Name.Returns("tool_a");
        var tool2 = Substitute.For<ITool>();
        tool2.Name.Returns("tool_b");

        var candidates = new List<ToolCandidate>
        {
            new(tool1, 0.5, 0.5, 0.5, 0.5, []),
            new(tool2, 0.5, 0.5, 0.5, 0.5, [])
        };

        var constraints = new List<Constraint>
        {
            Constraint.Create("no_a", "exclude:tool_a")
        };

        // Act
        var result = SmartToolSelector.ApplyConstraints(candidates, constraints);

        // Assert
        result.Should().HaveCount(1);
        result[0].Tool.Name.Should().Be("tool_b");
    }

    [Fact]
    public void ApplyConstraints_MaxCostConstraint_FiltersHighCostTools()
    {
        // Arrange
        var tool1 = Substitute.For<ITool>();
        tool1.Name.Returns("cheap");
        var tool2 = Substitute.For<ITool>();
        tool2.Name.Returns("expensive");

        var candidates = new List<ToolCandidate>
        {
            new(tool1, 0.5, 0.3, 0.5, 0.5, []),  // cost 0.3
            new(tool2, 0.5, 0.9, 0.5, 0.5, [])   // cost 0.9
        };

        var constraints = new List<Constraint>
        {
            Constraint.Create("budget", "max_cost:0.5")
        };

        // Act
        var result = SmartToolSelector.ApplyConstraints(candidates, constraints);

        // Assert
        result.Should().HaveCount(1);
        result[0].Tool.Name.Should().Be("cheap");
    }

    [Fact]
    public void ApplyConstraints_MinQualityConstraint_FiltersLowQualityTools()
    {
        // Arrange
        var goodTool = Substitute.For<ITool>();
        goodTool.Name.Returns("good");
        var badTool = Substitute.For<ITool>();
        badTool.Name.Returns("bad");

        var candidates = new List<ToolCandidate>
        {
            new(goodTool, 0.5, 0.5, 0.5, 0.9, []),  // quality 0.9
            new(badTool, 0.5, 0.5, 0.5, 0.2, [])     // quality 0.2
        };

        var constraints = new List<Constraint>
        {
            Constraint.Create("quality", "min_quality:0.5")
        };

        // Act
        var result = SmartToolSelector.ApplyConstraints(candidates, constraints);

        // Assert
        result.Should().HaveCount(1);
        result[0].Tool.Name.Should().Be("good");
    }

    [Fact]
    public void ApplyConstraints_RequireCapability_FiltersToolsWithoutCapability()
    {
        // Arrange
        var toolWithCap = Substitute.For<ITool>();
        toolWithCap.Name.Returns("capable");
        var toolWithoutCap = Substitute.For<ITool>();
        toolWithoutCap.Name.Returns("incapable");

        var candidates = new List<ToolCandidate>
        {
            new(toolWithCap, 0.5, 0.5, 0.5, 0.5, new[] { "search" }),
            new(toolWithoutCap, 0.5, 0.5, 0.5, 0.5, Array.Empty<string>())
        };

        var constraints = new List<Constraint>
        {
            Constraint.Create("need_search", "require:search")
        };

        // Act
        var result = SmartToolSelector.ApplyConstraints(candidates, constraints);

        // Assert
        result.Should().HaveCount(1);
        result[0].Tool.Name.Should().Be("capable");
    }

    [Fact]
    public void ApplyConstraints_UnknownConstraint_LeavesUnchanged()
    {
        // Arrange
        var tool = Substitute.For<ITool>();
        tool.Name.Returns("tool");
        var candidates = new List<ToolCandidate>
        {
            new(tool, 0.5, 0.5, 0.5, 0.5, [])
        };
        var constraints = new List<Constraint>
        {
            Constraint.Create("unknown", "some_unknown_rule:value")
        };

        // Act
        var result = SmartToolSelector.ApplyConstraints(candidates, constraints);

        // Assert
        result.Should().HaveCount(1);
    }

    #endregion

    #region IsToolAvailable

    [Fact]
    public void IsToolAvailable_ExistingUnconstrained_ReturnsTrue()
    {
        // Arrange
        var (selector, _) = CreateSelector(("my_tool", "A tool"));

        // Act & Assert
        selector.IsToolAvailable("my_tool").Should().BeTrue();
    }

    [Fact]
    public void IsToolAvailable_NonexistentTool_ReturnsFalse()
    {
        // Arrange
        var (selector, _) = CreateSelector();

        // Act & Assert
        selector.IsToolAvailable("nonexistent").Should().BeFalse();
    }

    [Fact]
    public void IsToolAvailable_ExcludedByConstraint_ReturnsFalse()
    {
        // Arrange
        var worldState = WorldState.Empty()
            .WithConstraint(Constraint.Create("block", "exclude:my_tool"));

        var registry = new ToolRegistry();
        var tool = Substitute.For<ITool>();
        tool.Name.Returns("my_tool");
        tool.Description.Returns("desc");
        registry.Register(tool);

        var matcher = new ToolCapabilityMatcher(registry);
        var selector = new SmartToolSelector(worldState, registry, matcher);

        // Act & Assert
        selector.IsToolAvailable("my_tool").Should().BeFalse();
    }

    #endregion

    #region GetAllCandidates

    [Fact]
    public void GetAllCandidates_ReturnsAllToolsWithDefaultScores()
    {
        // Arrange
        var (selector, _) = CreateSelector(("t1", "d1"), ("t2", "d2"));

        // Act
        var candidates = selector.GetAllCandidates();

        // Assert
        candidates.Should().HaveCount(2);
        candidates.Should().OnlyContain(c => c.FitScore == 0.5);
    }

    #endregion
}
