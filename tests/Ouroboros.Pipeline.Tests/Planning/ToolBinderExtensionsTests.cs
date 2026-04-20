using FluentAssertions;
using NSubstitute;
using Ouroboros.Abstractions;
using Ouroboros.Pipeline.Planning;
using Ouroboros.Tools;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Tests.Planning;

[Trait("Category", "Unit")]
public sealed class ToolBinderExtensionsTests
{
    private static (MeTTaPlanner Planner, ToolBinder Binder) CreatePlannerAndBinder(
        string planResult = "summarize_tool",
        string toolOutput = "processed")
    {
        var engine = Substitute.For<IMeTTaEngine>();
        engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string, string>.Success(planResult)));

        var planner = new MeTTaPlanner(engine);

        var tool = Substitute.For<ITool>();
        tool.Name.Returns("summarize_tool");
        tool.Description.Returns("Summarization tool");
        tool.InvokeAsync(Arg.Any<string>())
            .Returns(Task.FromResult(Result<string, string>.Success(toolOutput)));

        var registry = new ToolRegistry();
        registry.Register(tool);

        var binder = new ToolBinder(registry);

        return (planner, binder);
    }

    [Fact]
    public async Task PlanAndBindAsync_PlanSucceeds_ReturnsBindResult()
    {
        // Arrange
        var (planner, binder) = CreatePlannerAndBinder();

        // Act
        var result = await planner.PlanAndBindAsync(
            binder, MeTTaType.Text, MeTTaType.Summary);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task PlanAndBindAsync_PlanFails_PropagatesFailure()
    {
        // Arrange
        var engine = Substitute.For<IMeTTaEngine>();
        engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string, string>.Failure("No path")));
        var planner = new MeTTaPlanner(engine);
        var binder = new ToolBinder(new ToolRegistry());

        // Act
        var result = await planner.PlanAndBindAsync(
            binder, MeTTaType.Text, MeTTaType.Answer);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task PlanBindAndExecuteAsync_AllSucceeds_ReturnsOutput()
    {
        // Arrange
        var (planner, binder) = CreatePlannerAndBinder(
            planResult: "summarize_tool",
            toolOutput: "summarized text");

        // Act
        var result = await planner.PlanBindAndExecuteAsync(
            binder, "input text", MeTTaType.Text, MeTTaType.Summary);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("summarized text");
    }

    [Fact]
    public async Task PlanBindAndExecuteAsync_PlanFails_ReturnsFailure()
    {
        // Arrange
        var engine = Substitute.For<IMeTTaEngine>();
        engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string, string>.Failure("No chain")));
        var planner = new MeTTaPlanner(engine);
        var binder = new ToolBinder(new ToolRegistry());

        // Act
        var result = await planner.PlanBindAndExecuteAsync(
            binder, "input", MeTTaType.Text, MeTTaType.Code);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task PlanBindAndExecuteAsync_BindFails_ReturnsFailure()
    {
        // Arrange
        var engine = Substitute.For<IMeTTaEngine>();
        engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string, string>.Success("nonexistent_tool")));
        var planner = new MeTTaPlanner(engine);
        var binder = new ToolBinder(new ToolRegistry()); // empty registry

        // Act
        var result = await planner.PlanBindAndExecuteAsync(
            binder, "input", MeTTaType.Text, MeTTaType.Code);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task PlanBindAndExecuteAsync_ExecutionThrows_ReturnsFailure()
    {
        // Arrange
        var engine = Substitute.For<IMeTTaEngine>();
        engine.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string, string>.Success("bad_tool")));

        var tool = Substitute.For<ITool>();
        tool.Name.Returns("bad_tool");
        tool.InvokeAsync(Arg.Any<string>())
            .Returns(Task.FromResult(Result<string, string>.Failure("Tool failed")));

        var registry = new ToolRegistry();
        registry.Register(tool);

        var planner = new MeTTaPlanner(engine);
        var binder = new ToolBinder(registry);

        // Act
        var result = await planner.PlanBindAndExecuteAsync(
            binder, "input", MeTTaType.Text, MeTTaType.Code);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Execution failed");
    }
}
