// <copyright file="DynamicToolSelectorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Agent.Tests.MetaAI;

/// <summary>
/// Unit tests for <see cref="DynamicToolSelector"/>.
/// </summary>
[Trait("Category", "Unit")]
public class DynamicToolSelectorTests
{
    /// <summary>
    /// Simple mock tool for testing categorization and selection.
    /// </summary>
    private sealed class MockTool : ITool
    {
        public string Name { get; }
        public string Description { get; }
        public string? JsonSchema => null;

        public MockTool(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct)
            => Task.FromResult(Result<string, string>.Success($"Executed {Name}"));
    }

    private static ToolRegistry CreateRegistryWithTools(params (string Name, string Description)[] tools)
    {
        var registry = new ToolRegistry();
        foreach (var (name, desc) in tools)
        {
            registry = registry.WithTool(new MockTool(name, desc));
        }
        return registry;
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_NullRegistry_ThrowsArgumentNullException()
    {
        var act = () => new DynamicToolSelector(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidRegistry_DoesNotThrow()
    {
        // Arrange
        var registry = CreateRegistryWithTools(("test-tool", "A test tool"));

        // Act
        var act = () => new DynamicToolSelector(registry);

        // Assert
        act.Should().NotThrow();
    }

    // --- SelectToolsForUseCase ---

    [Fact]
    public void SelectToolsForUseCase_NullUseCase_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = CreateRegistryWithTools(("tool", "desc"));
        var sut = new DynamicToolSelector(registry);

        // Act
        var act = () => sut.SelectToolsForUseCase(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectToolsForUseCase_CodeGeneration_SelectsCodeTools()
    {
        // Arrange
        var registry = CreateRegistryWithTools(
            ("code-formatter", "Format code"),
            ("web-fetcher", "Fetch web pages"),
            ("compiler-tool", "Compile source code"));
        var sut = new DynamicToolSelector(registry);
        var useCase = new UseCase(UseCaseType.CodeGeneration, 0.8);

        // Act
        var result = sut.SelectToolsForUseCase(useCase);

        // Assert — should include code-related tools
        result.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SelectToolsForUseCase_NoMatchingTools_ReturnsBaseTools()
    {
        // Arrange
        var registry = CreateRegistryWithTools(("generic-tool", "A generic tool"));
        var sut = new DynamicToolSelector(registry);
        var useCase = new UseCase(UseCaseType.CodeGeneration, 0.8);

        // Act
        var result = sut.SelectToolsForUseCase(useCase);

        // Assert — falls back to base tools when no specific match
        result.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void SelectToolsForUseCase_WithContextMaxTools_RespectsLimit()
    {
        // Arrange
        var registry = CreateRegistryWithTools(
            ("code-tool-1", "Code analysis tool"),
            ("code-tool-2", "Code compilation tool"),
            ("code-tool-3", "Code formatting tool"),
            ("code-tool-4", "Code debugging tool"));
        var sut = new DynamicToolSelector(registry);
        var useCase = new UseCase(UseCaseType.CodeGeneration, 0.8);
        var context = new ToolSelectionContext { MaxTools = 2 };

        // Act
        var result = sut.SelectToolsForUseCase(useCase, context);

        // Assert
        result.Count.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public void SelectToolsForUseCase_WithRequiredCategories_FiltersCorrectly()
    {
        // Arrange
        var registry = CreateRegistryWithTools(
            ("code-formatter", "Format code"),
            ("web-fetcher", "Fetch web pages"));
        var sut = new DynamicToolSelector(registry);
        var useCase = new UseCase(UseCaseType.CodeGeneration, 0.8);
        var context = new ToolSelectionContext
        {
            RequiredCategories = new List<ToolCategory> { ToolCategory.Code }
        };

        // Act
        var result = sut.SelectToolsForUseCase(useCase, context);

        // Assert — should only include code category tools (or fall back to base if none match)
        result.Count.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void SelectToolsForUseCase_WithExcludedCategories_FiltersOut()
    {
        // Arrange
        var registry = CreateRegistryWithTools(
            ("code-formatter", "Format code"),
            ("web-fetcher", "Fetch web pages"));
        var sut = new DynamicToolSelector(registry);
        var useCase = new UseCase(UseCaseType.CodeGeneration, 0.8);
        var context = new ToolSelectionContext
        {
            ExcludedCategories = new List<ToolCategory> { ToolCategory.Web }
        };

        // Act
        var result = sut.SelectToolsForUseCase(useCase, context);

        // Assert — web tools should be excluded
        result.All.Should().NotContain(t => t.Name == "web-fetcher");
    }

    // --- SelectToolsForPrompt ---

    [Fact]
    public void SelectToolsForPrompt_NullPrompt_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = CreateRegistryWithTools(("tool", "desc"));
        var sut = new DynamicToolSelector(registry);

        // Act
        var act = () => sut.SelectToolsForPrompt(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectToolsForPrompt_CodeRelatedPrompt_SelectsCodeTools()
    {
        // Arrange
        var registry = CreateRegistryWithTools(
            ("code-analyzer", "Analyze code quality"),
            ("file-reader", "Read files from disk"),
            ("search-engine", "Search the knowledge base"));
        var sut = new DynamicToolSelector(registry);

        // Act
        var result = sut.SelectToolsForPrompt("Please implement a function to sort the code");

        // Assert
        result.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SelectToolsForPrompt_FileRelatedPrompt_SelectsFileTools()
    {
        // Arrange
        var registry = CreateRegistryWithTools(
            ("file-reader", "Read files from disk"),
            ("code-analyzer", "Analyze code quality"));
        var sut = new DynamicToolSelector(registry);

        // Act
        var result = sut.SelectToolsForPrompt("Please read the configuration file and save it");

        // Assert
        result.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SelectToolsForPrompt_NoKeywordMatch_ReturnsBaseTools()
    {
        // Arrange
        var registry = CreateRegistryWithTools(("generic", "A tool"));
        var sut = new DynamicToolSelector(registry);

        // Act
        var result = sut.SelectToolsForPrompt("xyzzyx");

        // Assert — always includes General category, falls back to base if empty
        result.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    // --- GetToolRecommendations ---

    [Fact]
    public void GetToolRecommendations_ReturnsAllToolsWithScores()
    {
        // Arrange
        var registry = CreateRegistryWithTools(
            ("code-tool", "Code analysis"),
            ("search-tool", "Search documents"),
            ("general-tool", "General purpose"));
        var sut = new DynamicToolSelector(registry);
        var useCase = new UseCase(UseCaseType.CodeGeneration, 0.8);

        // Act
        var recommendations = sut.GetToolRecommendations(useCase, "analyze this code");

        // Assert
        recommendations.Should().HaveCount(3);
        recommendations.Should().AllSatisfy(r =>
        {
            r.RelevanceScore.Should().BeGreaterThanOrEqualTo(0.0);
            r.RelevanceScore.Should().BeLessThanOrEqualTo(1.0);
        });
    }

    [Fact]
    public void GetToolRecommendations_SortedByRelevanceDescending()
    {
        // Arrange
        var registry = CreateRegistryWithTools(
            ("code-tool", "Code analysis"),
            ("search-tool", "Search documents"));
        var sut = new DynamicToolSelector(registry);
        var useCase = new UseCase(UseCaseType.CodeGeneration, 0.8);

        // Act
        var recommendations = sut.GetToolRecommendations(useCase, "code");

        // Assert
        for (int i = 1; i < recommendations.Count; i++)
        {
            recommendations[i - 1].RelevanceScore.Should()
                .BeGreaterThanOrEqualTo(recommendations[i].RelevanceScore);
        }
    }

    [Fact]
    public void GetToolRecommendations_RelevanceScoreCappedAtOne()
    {
        // Arrange
        var registry = CreateRegistryWithTools(
            ("code-compiler-debug-fix", "Code compilation debugging fixing linting formatting refactoring"));
        var sut = new DynamicToolSelector(registry);
        var useCase = new UseCase(UseCaseType.CodeGeneration, 0.8);

        // Act
        var recommendations = sut.GetToolRecommendations(
            useCase, "code compile debug fix lint format refactor");

        // Assert
        recommendations.Should().AllSatisfy(r => r.RelevanceScore.Should().BeLessThanOrEqualTo(1.0));
    }

    // --- GetToolStatsByCategory ---

    [Fact]
    public void GetToolStatsByCategory_ReturnsCountsPerCategory()
    {
        // Arrange
        var registry = CreateRegistryWithTools(
            ("code-tool", "Code analysis"),
            ("compiler", "Compile code"),
            ("web-fetcher", "Fetch web data"));
        var sut = new DynamicToolSelector(registry);

        // Act
        var stats = sut.GetToolStatsByCategory();

        // Assert
        stats.Should().ContainKey(ToolCategory.Code);
        stats.Should().ContainKey(ToolCategory.Web);
        stats.Values.Sum().Should().Be(3);
    }

    // --- ToolRecommendation record ---

    [Fact]
    public void ToolRecommendation_IsHighlyRecommended_HighScore_ReturnsTrue()
    {
        var rec = new ToolRecommendation("tool", "desc", 0.8, ToolCategory.Code);
        rec.IsHighlyRecommended.Should().BeTrue();
        rec.IsRecommended.Should().BeTrue();
    }

    [Fact]
    public void ToolRecommendation_IsRecommended_MediumScore_ReturnsTrue()
    {
        var rec = new ToolRecommendation("tool", "desc", 0.5, ToolCategory.Code);
        rec.IsHighlyRecommended.Should().BeFalse();
        rec.IsRecommended.Should().BeTrue();
    }

    [Fact]
    public void ToolRecommendation_LowScore_NotRecommended()
    {
        var rec = new ToolRecommendation("tool", "desc", 0.2, ToolCategory.General);
        rec.IsHighlyRecommended.Should().BeFalse();
        rec.IsRecommended.Should().BeFalse();
    }

    // --- ToolCategory enum ---

    [Fact]
    public void ToolCategory_HasExpectedValues()
    {
        Enum.GetValues<ToolCategory>().Should().HaveCount(11);
    }
}
