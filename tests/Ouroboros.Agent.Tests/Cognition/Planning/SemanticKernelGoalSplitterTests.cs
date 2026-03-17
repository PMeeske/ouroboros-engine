// <copyright file="SemanticKernelGoalSplitterTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Abstractions.Core;
using Ouroboros.Agent.Cognition.Planning;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests.Cognition.Planning;

/// <summary>
/// Unit tests for <see cref="SemanticKernelGoalSplitter"/> and <see cref="GoalSplitterConfig"/>.
/// </summary>
[Trait("Category", "Unit")]
public class SemanticKernelGoalSplitterTests
{
    private readonly Mock<IChatCompletionModel> _llmMock = new();
    private readonly Mock<IEthicsFramework> _ethicsMock = new();

    // --- Constructor ---

    [Fact]
    public void Constructor_NullLlm_ThrowsArgumentNullException()
    {
        var act = () => new SemanticKernelGoalSplitter(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidLlm_DoesNotThrow()
    {
        var act = () => new SemanticKernelGoalSplitter(_llmMock.Object);

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithAllParameters_DoesNotThrow()
    {
        var router = new HypergridRouter();
        var config = new GoalSplitterConfig(MaxSteps: 5, MaxRetries: 1);

        var act = () => new SemanticKernelGoalSplitter(
            _llmMock.Object, router, _ethicsMock.Object, config);

        act.Should().NotThrow();
    }

    // --- SplitAsync ---

    [Fact]
    public async Task SplitAsync_WhenLlmReturnsValidJson_ReturnsSteps()
    {
        // Arrange
        _llmMock
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                [
                    {"description": "Design API", "type": "Instrumental", "priority": 0.8, "dependsOn": []},
                    {"description": "Implement endpoints", "type": "Primary", "priority": 0.9, "dependsOn": [0]}
                ]
            """);

        var sut = new SemanticKernelGoalSplitter(_llmMock.Object);
        var goal = new Goal("Build REST API", GoalType.Primary, 0.9);

        // Act
        var result = await sut.SplitAsync(goal, HypergridContext.Default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Steps.Should().HaveCount(2);
        result.Value.Steps[0].Description.Should().Be("Design API");
        result.Value.Steps[1].Description.Should().Be("Implement endpoints");
        result.Value.OriginalGoal.Should().Be(goal);
        result.Value.DimensionalAnalysis.Should().NotBeNull();
    }

    [Fact]
    public async Task SplitAsync_WhenLlmReturnsEmptyArray_ReturnsFailure()
    {
        // Arrange
        _llmMock
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("[]");

        var sut = new SemanticKernelGoalSplitter(_llmMock.Object);
        var goal = new Goal("Build something", GoalType.Primary, 0.5);

        // Act
        var result = await sut.SplitAsync(goal, HypergridContext.Default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("no steps");
    }

    [Fact]
    public async Task SplitAsync_WhenLlmReturnsInvalidJson_ReturnsFailure()
    {
        // Arrange
        _llmMock
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("This is not JSON at all");

        var sut = new SemanticKernelGoalSplitter(_llmMock.Object);
        var goal = new Goal("Build something", GoalType.Primary, 0.5);

        // Act
        var result = await sut.SplitAsync(goal, HypergridContext.Default);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SplitAsync_WhenLlmThrows_ReturnsFailure()
    {
        // Arrange
        _llmMock
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        var sut = new SemanticKernelGoalSplitter(_llmMock.Object);
        var goal = new Goal("Build something", GoalType.Primary, 0.5);

        // Act
        var result = await sut.SplitAsync(goal, HypergridContext.Default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Goal splitting failed");
    }

    [Fact]
    public async Task SplitAsync_WhenCancelled_ThrowsOperationCancelledException()
    {
        // Arrange
        _llmMock
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var sut = new SemanticKernelGoalSplitter(_llmMock.Object);
        var goal = new Goal("Build something", GoalType.Primary, 0.5);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => sut.SplitAsync(goal, HypergridContext.Default));
    }

    [Fact]
    public async Task SplitAsync_WithMarkdownFencedJson_ParsesCorrectly()
    {
        // Arrange — LLMs often wrap JSON in markdown code fences
        _llmMock
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                ```json
                [
                    {"description": "Step 1", "type": "Primary", "priority": 0.9, "dependsOn": []}
                ]
                ```
            """);

        var sut = new SemanticKernelGoalSplitter(_llmMock.Object);
        var goal = new Goal("Do task", GoalType.Primary, 0.8);

        // Act
        var result = await sut.SplitAsync(goal, HypergridContext.Default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Steps.Should().HaveCount(1);
    }

    [Fact]
    public async Task SplitAsync_WithContextSkillsAndTools_IncludesInPrompt()
    {
        // Arrange
        string capturedPrompt = "";
        _llmMock
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((prompt, _) => capturedPrompt = prompt)
            .ReturnsAsync("""[{"description": "Use tool", "type": "Instrumental", "priority": 0.5}]""");

        var context = new HypergridContext(
            Deadline: DateTimeOffset.UtcNow.AddHours(1),
            AvailableSkills: new List<string> { "coding" },
            AvailableTools: new List<string> { "browser" });

        var sut = new SemanticKernelGoalSplitter(_llmMock.Object);
        var goal = new Goal("Research topic", GoalType.Primary, 0.7);

        // Act
        await sut.SplitAsync(goal, context);

        // Assert
        capturedPrompt.Should().Contain("coding");
        capturedPrompt.Should().Contain("browser");
        capturedPrompt.Should().Contain("Deadline");
    }

    [Fact]
    public async Task SplitAsync_ClampsPriorityToValidRange()
    {
        // Arrange — priority > 1.0 should be clamped
        _llmMock
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                [
                    {"description": "Over-priority", "type": "Primary", "priority": 2.5, "dependsOn": []},
                    {"description": "Under-priority", "type": "Secondary", "priority": -0.5, "dependsOn": []}
                ]
            """);

        var sut = new SemanticKernelGoalSplitter(_llmMock.Object);
        var goal = new Goal("Test clamping", GoalType.Primary, 0.5);

        // Act
        var result = await sut.SplitAsync(goal, HypergridContext.Default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Steps[0].Priority.Should().Be(1.0);
        result.Value.Steps[1].Priority.Should().Be(0.0);
    }

    [Fact]
    public async Task SplitAsync_RespectsMaxStepsConfig()
    {
        // Arrange — return 10 steps but config limits to 3
        var steps = Enumerable.Range(0, 10)
            .Select(i => $$"""{"description": "Step {{i}}", "type": "Primary", "priority": 0.5}""");
        string json = $"[{string.Join(",", steps)}]";

        _llmMock
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);

        var config = new GoalSplitterConfig(MaxSteps: 3);
        var sut = new SemanticKernelGoalSplitter(_llmMock.Object, config: config);
        var goal = new Goal("Many steps", GoalType.Primary, 0.5);

        // Act
        var result = await sut.SplitAsync(goal, HypergridContext.Default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Steps.Should().HaveCountLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task SplitAsync_WithUnknownGoalType_DefaultsToInstrumental()
    {
        // Arrange
        _llmMock
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""[{"description": "Unknown type step", "type": "NonExistent", "priority": 0.5}]""");

        var sut = new SemanticKernelGoalSplitter(_llmMock.Object);
        var goal = new Goal("Test", GoalType.Primary, 0.5);

        // Act
        var result = await sut.SplitAsync(goal, HypergridContext.Default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Steps[0].Type.Should().Be(GoalType.Instrumental);
    }

    // --- GoalSplitterConfig ---

    [Fact]
    public void GoalSplitterConfig_Defaults_AreReasonable()
    {
        var config = new GoalSplitterConfig();

        config.MaxSteps.Should().Be(8);
        config.MaxRetries.Should().Be(2);
    }

    [Fact]
    public void GoalSplitterConfig_WithCustomValues_SetsProperties()
    {
        var config = new GoalSplitterConfig(MaxSteps: 12, MaxRetries: 5);

        config.MaxSteps.Should().Be(12);
        config.MaxRetries.Should().Be(5);
    }
}
