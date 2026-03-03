using FluentAssertions;
using Moq;
using Ouroboros.Abstractions.Core;
using Ouroboros.Providers;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests.Providers;

[Trait("Category", "Unit")]
public sealed class ToolAwareChatModelTests
{
    private readonly Mock<IChatCompletionModel> _llmMock = new();
    private readonly ToolRegistry _registry = new();

    [Fact]
    public void InnerModel_ReturnsProvidedModel()
    {
        var sut = new ToolAwareChatModel(_llmMock.Object, _registry);
        sut.InnerModel.Should().BeSameAs(_llmMock.Object);
    }

    [Fact]
    public void SupportsThinking_NonThinkingModel_ReturnsFalse()
    {
        var sut = new ToolAwareChatModel(_llmMock.Object, _registry);
        sut.SupportsThinking.Should().BeFalse();
    }

    [Fact]
    public void SupportsThinking_ThinkingModel_ReturnsTrue()
    {
        var thinkingMock = new Mock<IChatCompletionModel>();
        thinkingMock.As<IThinkingChatModel>();

        var sut = new ToolAwareChatModel(thinkingMock.Object, _registry);
        sut.SupportsThinking.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateWithToolsAsync_NoToolCalls_ReturnsCleanText()
    {
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("plain text response");

        var sut = new ToolAwareChatModel(_llmMock.Object, _registry);
        var (text, tools) = await sut.GenerateWithToolsAsync("test prompt");

        text.Should().Be("plain text response");
        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateWithToolsAsync_UnknownTool_ReturnsError()
    {
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Use [TOOL:unknown_tool some args] to do something");

        var sut = new ToolAwareChatModel(_llmMock.Object, _registry);
        var (text, tools) = await sut.GenerateWithToolsAsync("test");

        text.Should().Contain("[TOOL-RESULT:unknown_tool] error: tool not found");
    }

    [Fact]
    public async Task GenerateWithToolsAsync_RegisteredTool_ExecutesAndReplacesInText()
    {
        var toolMock = new Mock<ITool>();
        toolMock.Setup(t => t.Name).Returns("calculator");
        toolMock.Setup(t => t.InvokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success("42"));

        _registry.Register(toolMock.Object);

        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("The answer is [TOOL:calculator 6*7].");

        var sut = new ToolAwareChatModel(_llmMock.Object, _registry);
        var (text, tools) = await sut.GenerateWithToolsAsync("Calculate 6*7");

        text.Should().Contain("[TOOL-RESULT:calculator] 42");
        tools.Should().HaveCount(1);
        tools[0].ToolName.Should().Be("calculator");
        tools[0].Output.Should().Be("42");
    }

    [Fact]
    public async Task GenerateWithToolsAsync_ToolThrows_ReturnsError()
    {
        var toolMock = new Mock<ITool>();
        toolMock.Setup(t => t.Name).Returns("broken");
        toolMock.Setup(t => t.InvokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("tool broke"));

        _registry.Register(toolMock.Object);

        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("[TOOL:broken args]");

        var sut = new ToolAwareChatModel(_llmMock.Object, _registry);
        var (text, tools) = await sut.GenerateWithToolsAsync("test");

        text.Should().Contain("error: tool broke");
    }

    [Fact]
    public async Task GenerateWithToolsAsync_BeforeInvoke_DeniesExecution()
    {
        var toolMock = new Mock<ITool>();
        toolMock.Setup(t => t.Name).Returns("risky");

        _registry.Register(toolMock.Object);

        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("[TOOL:risky args]");

        var sut = new ToolAwareChatModel(_llmMock.Object, _registry);
        sut.BeforeInvoke = (name, args, ct) => Task.FromResult(false);

        var (text, tools) = await sut.GenerateWithToolsAsync("test");

        text.Should().Contain("denied by user");
        toolMock.Verify(t => t.InvokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateWithToolsAsync_AfterInvoke_Called()
    {
        var toolMock = new Mock<ITool>();
        toolMock.Setup(t => t.Name).Returns("calc");
        toolMock.Setup(t => t.InvokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success("result"));

        _registry.Register(toolMock.Object);

        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("[TOOL:calc x]");

        string? afterToolName = null;
        var sut = new ToolAwareChatModel(_llmMock.Object, _registry);
        sut.AfterInvoke = (name, args, output, elapsed, success) => afterToolName = name;

        await sut.GenerateWithToolsAsync("test");

        afterToolName.Should().Be("calc");
    }

    [Fact]
    public async Task GenerateWithToolsResultAsync_Success_ReturnsSuccessResult()
    {
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("no tools here");

        var sut = new ToolAwareChatModel(_llmMock.Object, _registry);
        var result = await sut.GenerateWithToolsResultAsync("test");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateWithToolsResultAsync_LlmThrows_ReturnsFailure()
    {
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("llm down"));

        var sut = new ToolAwareChatModel(_llmMock.Object, _registry);
        var result = await sut.GenerateWithToolsResultAsync("test");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateWithThinkingAndToolsAsync_NonThinkingModel_FallsBackToRegular()
    {
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("regular response");

        var sut = new ToolAwareChatModel(_llmMock.Object, _registry);
        var (response, tools) = await sut.GenerateWithThinkingAndToolsAsync("test");

        response.Content.Should().Be("regular response");
        response.HasThinking.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateWithThinkingAndToolsAsync_ThinkingModel_UsesThinking()
    {
        var thinkingMock = new Mock<IChatCompletionModel>();
        thinkingMock.As<IThinkingChatModel>()
            .Setup(m => m.GenerateWithThinkingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ThinkingResponse("my thoughts", "content"));

        var sut = new ToolAwareChatModel(thinkingMock.Object, _registry);
        var (response, tools) = await sut.GenerateWithThinkingAndToolsAsync("test");

        response.Thinking.Should().Be("my thoughts");
        response.Content.Should().Be("content");
    }

    [Fact]
    public async Task GenerateWithThinkingAndToolsResultAsync_Success_ReturnsSuccessResult()
    {
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ok");

        var sut = new ToolAwareChatModel(_llmMock.Object, _registry);
        var result = await sut.GenerateWithThinkingAndToolsResultAsync("test");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateWithThinkingAndToolsResultAsync_Failure_ReturnsFailureResult()
    {
        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var sut = new ToolAwareChatModel(_llmMock.Object, _registry);
        var result = await sut.GenerateWithThinkingAndToolsResultAsync("test");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateWithToolsAsync_MultipleTools_ExecutesAll()
    {
        var tool1 = new Mock<ITool>();
        tool1.Setup(t => t.Name).Returns("tool1");
        tool1.Setup(t => t.InvokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success("result1"));

        var tool2 = new Mock<ITool>();
        tool2.Setup(t => t.Name).Returns("tool2");
        tool2.Setup(t => t.InvokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success("result2"));

        _registry.Register(tool1.Object);
        _registry.Register(tool2.Object);

        _llmMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("First [TOOL:tool1 a] then [TOOL:tool2 b]");

        var sut = new ToolAwareChatModel(_llmMock.Object, _registry);
        var (text, tools) = await sut.GenerateWithToolsAsync("test");

        tools.Should().HaveCount(2);
        text.Should().Contain("[TOOL-RESULT:tool1] result1");
        text.Should().Contain("[TOOL-RESULT:tool2] result2");
    }
}
