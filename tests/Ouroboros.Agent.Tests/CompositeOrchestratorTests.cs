using FluentAssertions;
using Moq;
using Ouroboros.Agent;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class CompositeOrchestratorTests
{
    [Fact]
    public void FromFunc_CreatesOrchestrator()
    {
        var orchestrator = CompositeOrchestrator<string, string>.FromFunc(
            "test",
            (input, ctx) => Task.FromResult(input.ToUpper()));

        orchestrator.Should().NotBeNull();
    }

    [Fact]
    public async Task FromFunc_ExecutesFunction()
    {
        var orchestrator = CompositeOrchestrator<string, string>.FromFunc(
            "test",
            (input, ctx) => Task.FromResult(input.ToUpper()));

        var result = await orchestrator.ExecuteAsync("hello");

        result.Success.Should().BeTrue();
        result.Output.Should().Be("HELLO");
    }

    [Fact]
    public void FromFunc_NullFunc_Throws()
    {
        var act = () => CompositeOrchestrator<string, string>.FromFunc(
            "test",
            null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void From_NullOrchestrator_Throws()
    {
        var act = () => CompositeOrchestrator<string, string>.From(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Map_TransformsOutput()
    {
        var orchestrator = CompositeOrchestrator<string, int>.FromFunc(
            "test",
            (input, ctx) => Task.FromResult(input.Length));

        var mapped = orchestrator.Map(len => len * 2);
        var result = await mapped.ExecuteAsync("hello");

        result.Success.Should().BeTrue();
        result.Output.Should().Be(10);
    }

    [Fact]
    public void Map_NullMapper_Throws()
    {
        var orchestrator = CompositeOrchestrator<string, int>.FromFunc(
            "test",
            (input, ctx) => Task.FromResult(input.Length));

        var act = () => orchestrator.Map<string>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Tap_ExecutesSideEffect()
    {
        string? captured = null;
        var orchestrator = CompositeOrchestrator<string, string>.FromFunc(
            "test",
            (input, ctx) => Task.FromResult(input));

        var tapped = orchestrator.Tap(output => captured = output);
        var result = await tapped.ExecuteAsync("hello");

        result.Success.Should().BeTrue();
        captured.Should().Be("hello");
    }

    [Fact]
    public void Tap_NullAction_Throws()
    {
        var orchestrator = CompositeOrchestrator<string, string>.FromFunc(
            "test",
            (input, ctx) => Task.FromResult(input));

        var act = () => orchestrator.Tap(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Then_ChainsOrchestrators()
    {
        var first = CompositeOrchestrator<string, int>.FromFunc(
            "first",
            (input, ctx) => Task.FromResult(input.Length));

        var second = CompositeOrchestrator<int, string>.FromFunc(
            "second",
            (input, ctx) => Task.FromResult($"Length={input}"));

        var chained = first.Then(second);
        var result = await chained.ExecuteAsync("hello");

        result.Success.Should().BeTrue();
        result.Output.Should().Be("Length=5");
    }

    [Fact]
    public void Then_NullNext_Throws()
    {
        var first = CompositeOrchestrator<string, string>.FromFunc(
            "first",
            (input, ctx) => Task.FromResult(input));

        var act = () => first.Then<int>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_NullInput_ReturnsFailure()
    {
        var orchestrator = CompositeOrchestrator<string, string>.FromFunc(
            "test",
            (input, ctx) => Task.FromResult(input));

        var result = await orchestrator.ExecuteAsync(null!);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("null");
    }

    [Fact]
    public async Task ExecuteAsync_FunctionThrows_ReturnsFailure()
    {
        var orchestrator = CompositeOrchestrator<string, string>.FromFunc(
            "test",
            (input, ctx) => throw new InvalidOperationException("boom"));

        var result = await orchestrator.ExecuteAsync("hello");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("boom");
    }

    [Fact]
    public async Task From_WrapsExistingOrchestrator()
    {
        var inner = CompositeOrchestrator<string, string>.FromFunc(
            "inner",
            (input, ctx) => Task.FromResult(input.ToUpper()));

        var wrapped = CompositeOrchestrator<string, string>.From(inner);
        var result = await wrapped.ExecuteAsync("hello");

        result.Success.Should().BeTrue();
        result.Output.Should().Be("HELLO");
    }
}
