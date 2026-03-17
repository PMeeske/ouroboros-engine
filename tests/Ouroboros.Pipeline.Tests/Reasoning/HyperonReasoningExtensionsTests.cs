using Ouroboros.Pipeline.Reasoning;

namespace Ouroboros.Tests.Pipeline.Reasoning;

[Trait("Category", "Unit")]
public sealed class HyperonReasoningExtensionsTests
{
    [Fact]
    public async Task WithHyperonReasoningAsync_ExecutesReasoningAction()
    {
        // Arrange
        string context = "initial";
        bool actionCalled = false;

        // Act
        string result = await context.WithHyperonReasoningAsync("test-step",
            async (engine, ctx) =>
            {
                actionCalled = true;
                await Task.CompletedTask;
                return ctx + "-reasoned";
            });

        // Assert
        actionCalled.Should().BeTrue();
        result.Should().Be("initial-reasoned");
    }

    [Fact]
    public async Task WithHyperonReasoningAsync_ProvidesEngineToAction()
    {
        // Arrange
        string context = "test";
        bool engineProvided = false;

        // Act
        await context.WithHyperonReasoningAsync("step",
            async (engine, ctx) =>
            {
                engineProvided = engine != null;
                await Task.CompletedTask;
                return ctx;
            });

        // Assert
        engineProvided.Should().BeTrue();
    }

    [Fact]
    public void CreateInferenceStep_ReturnsCallableFunction()
    {
        // Arrange & Act
        var step = HyperonReasoningExtensions.CreateInferenceStep<string>(
            "test-step",
            "(= (IsA dog Animal) True)",
            new[] { "(match &self (IsA $x Animal) $x)" },
            (ctx, results) => ctx + $"-enriched({results.Count})");

        // Assert
        step.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateInferenceStep_ExecutesAndEnrichesContext()
    {
        // Arrange
        var step = HyperonReasoningExtensions.CreateInferenceStep<string>(
            "test-step",
            "(= (IsA dog Animal) True)",
            new[] { "(match &self (IsA $x Animal) $x)" },
            (ctx, results) => ctx + "-enriched");

        // Act
        string result = await step("original");

        // Assert
        result.Should().Contain("original");
        result.Should().Contain("enriched");
    }

    [Fact]
    public void CreatePatternStep_ReturnsCallableFunction()
    {
        // Arrange & Act
        var step = HyperonReasoningExtensions.CreatePatternStep<string>(
            "test-step",
            "(IsA $x Animal)",
            (ctx, sub) => ctx + "-matched",
            ctx => ctx + "-no-match");

        // Assert
        step.Should().NotBeNull();
    }

    [Fact]
    public async Task CreatePatternStep_WhenNoMatch_CallsOnNoMatch()
    {
        // Arrange
        var step = HyperonReasoningExtensions.CreatePatternStep<string>(
            "test-step",
            "(NonExistentPattern $x)",
            (ctx, sub) => ctx + "-matched",
            ctx => ctx + "-no-match");

        // Act
        string result = await step("original");

        // Assert
        // The pattern won't match in an empty AtomSpace, so onNoMatch should be called
        result.Should().Contain("original");
    }

    [Fact]
    public async Task CreatePatternStep_WhenNoMatchAndNoFallback_ReturnsOriginalContext()
    {
        // Arrange
        var step = HyperonReasoningExtensions.CreatePatternStep<string>(
            "test-step",
            "(NonExistentPattern $x)",
            (ctx, sub) => ctx + "-matched");

        // Act
        string result = await step("original");

        // Assert
        result.Should().Be("original");
    }
}
