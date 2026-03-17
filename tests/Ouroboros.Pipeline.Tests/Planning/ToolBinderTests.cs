using FluentAssertions;
using NSubstitute;
using Ouroboros.Pipeline.Planning;
using Ouroboros.Tools;

namespace Ouroboros.Tests.Planning;

[Trait("Category", "Unit")]
public sealed class ToolBinderTests
{
    private static ToolRegistry CreateRegistryWithTools(params (string Name, string Output)[] tools)
    {
        var registry = new ToolRegistry();
        foreach (var (name, output) in tools)
        {
            var tool = Substitute.For<ITool>();
            tool.Name.Returns(name);
            tool.Description.Returns($"Description for {name}");
            tool.InvokeAsync(Arg.Any<string>())
                .Returns(callInfo => Task.FromResult(Result<string, string>.Success(output)));
            registry.Register(tool);
        }
        return registry;
    }

    [Fact]
    public void Constructor_NullRegistry_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ToolBinder(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #region Bind

    [Fact]
    public void Bind_NullChain_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new ToolRegistry();
        var binder = new ToolBinder(registry);

        // Act
        var act = () => binder.Bind(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Bind_EmptyChain_ReturnsFailure()
    {
        // Arrange
        var registry = new ToolRegistry();
        var binder = new ToolBinder(registry);

        // Act
        var result = binder.Bind(ToolChain.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public void Bind_MissingTool_ReturnsFailure()
    {
        // Arrange
        var registry = new ToolRegistry();
        var binder = new ToolBinder(registry);
        var chain = new ToolChain(new[] { "nonexistent_tool" });

        // Act
        var result = binder.Bind(chain);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("nonexistent_tool");
    }

    [Fact]
    public void Bind_AllToolsExist_ReturnsSuccess()
    {
        // Arrange
        var registry = CreateRegistryWithTools(
            ("tool_a", "output_a"),
            ("tool_b", "output_b"));
        var binder = new ToolBinder(registry);
        var chain = new ToolChain(new[] { "tool_a", "tool_b" });

        // Act
        var result = binder.Bind(chain);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Bind_ExecutesPipeline_ChainsToolsInOrder()
    {
        // Arrange
        var toolA = Substitute.For<ITool>();
        toolA.Name.Returns("tool_a");
        toolA.InvokeAsync(Arg.Any<string>())
            .Returns(callInfo => Task.FromResult(
                Result<string, string>.Success($"[A:{callInfo.Arg<string>()}]")));

        var toolB = Substitute.For<ITool>();
        toolB.Name.Returns("tool_b");
        toolB.InvokeAsync(Arg.Any<string>())
            .Returns(callInfo => Task.FromResult(
                Result<string, string>.Success($"[B:{callInfo.Arg<string>()}]")));

        var registry = new ToolRegistry();
        registry.Register(toolA);
        registry.Register(toolB);

        var binder = new ToolBinder(registry);
        var chain = new ToolChain(new[] { "tool_a", "tool_b" });

        // Act
        var bindResult = binder.Bind(chain);
        var output = await bindResult.Value("input");

        // Assert
        output.Should().Be("[B:[A:input]]");
    }

    [Fact]
    public async Task Bind_ToolFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var tool = Substitute.For<ITool>();
        tool.Name.Returns("failing_tool");
        tool.InvokeAsync(Arg.Any<string>())
            .Returns(Task.FromResult(Result<string, string>.Failure("Tool error")));

        var registry = new ToolRegistry();
        registry.Register(tool);

        var binder = new ToolBinder(registry);
        var chain = new ToolChain(new[] { "failing_tool" });

        // Act
        var bindResult = binder.Bind(chain);
        var act = () => bindResult.Value("input");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*failing_tool*failed*");
    }

    #endregion

    #region BindSafe

    [Fact]
    public void BindSafe_NullChain_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new ToolRegistry();
        var binder = new ToolBinder(registry);

        // Act
        var act = () => binder.BindSafe(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BindSafe_EmptyChain_ReturnsFailure()
    {
        // Arrange
        var registry = new ToolRegistry();
        var binder = new ToolBinder(registry);

        // Act
        var result = binder.BindSafe(ToolChain.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void BindSafe_MissingTool_ReturnsFailure()
    {
        // Arrange
        var registry = new ToolRegistry();
        var binder = new ToolBinder(registry);
        var chain = new ToolChain(new[] { "missing" });

        // Act
        var result = binder.BindSafe(chain);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task BindSafe_ToolFails_ReturnsFailureResult()
    {
        // Arrange
        var tool = Substitute.For<ITool>();
        tool.Name.Returns("fail_tool");
        tool.InvokeAsync(Arg.Any<string>())
            .Returns(Task.FromResult(Result<string, string>.Failure("Oops")));

        var registry = new ToolRegistry();
        registry.Register(tool);

        var binder = new ToolBinder(registry);
        var chain = new ToolChain(new[] { "fail_tool" });

        // Act
        var bindResult = binder.BindSafe(chain);
        bindResult.IsSuccess.Should().BeTrue();
        var execResult = await bindResult.Value("input");

        // Assert
        execResult.IsFailure.Should().BeTrue();
        execResult.Error.Should().Contain("fail_tool");
    }

    [Fact]
    public async Task BindSafe_AllToolsSucceed_ReturnsSuccessResult()
    {
        // Arrange
        var tool = Substitute.For<ITool>();
        tool.Name.Returns("good_tool");
        tool.InvokeAsync(Arg.Any<string>())
            .Returns(Task.FromResult(Result<string, string>.Success("done")));

        var registry = new ToolRegistry();
        registry.Register(tool);

        var binder = new ToolBinder(registry);
        var chain = new ToolChain(new[] { "good_tool" });

        // Act
        var bindResult = binder.BindSafe(chain);
        var execResult = await bindResult.Value("input");

        // Assert
        execResult.IsSuccess.Should().BeTrue();
        execResult.Value.Should().Be("done");
    }

    #endregion

    #region BindWithProgress

    [Fact]
    public void BindWithProgress_NullChain_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new ToolRegistry();
        var binder = new ToolBinder(registry);

        // Act
        var act = () => binder.BindWithProgress(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BindWithProgress_EmptyChain_ReturnsFailure()
    {
        // Arrange
        var registry = new ToolRegistry();
        var binder = new ToolBinder(registry);

        // Act
        var result = binder.BindWithProgress(ToolChain.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task BindWithProgress_ReportsProgressCorrectly()
    {
        // Arrange
        var reports = new List<(string ToolName, int Index, int Total)>();
        var progress = new Progress<(string ToolName, int Index, int Total)>(report => reports.Add(report));

        var tool1 = Substitute.For<ITool>();
        tool1.Name.Returns("step1");
        tool1.InvokeAsync(Arg.Any<string>())
            .Returns(Task.FromResult(Result<string, string>.Success("step1_out")));

        var tool2 = Substitute.For<ITool>();
        tool2.Name.Returns("step2");
        tool2.InvokeAsync(Arg.Any<string>())
            .Returns(Task.FromResult(Result<string, string>.Success("step2_out")));

        var registry = new ToolRegistry();
        registry.Register(tool1);
        registry.Register(tool2);

        var binder = new ToolBinder(registry);
        var chain = new ToolChain(new[] { "step1", "step2" });

        // Act
        var bindResult = binder.BindWithProgress(chain, progress);
        bindResult.IsSuccess.Should().BeTrue();
        var output = await bindResult.Value("input");

        // Assert
        output.Should().Be("step2_out");
        // Progress reporting is async via IProgress, may need a small delay
        await Task.Delay(50);
        reports.Should().HaveCount(2);
    }

    #endregion
}
