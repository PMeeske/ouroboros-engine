using FluentAssertions;
using Ouroboros.Core.Monads;
using Ouroboros.Providers;
using Ouroboros.Tests.Mocks;
using Ouroboros.Tests.Providers;
using Ouroboros.Tools;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ToolAwareChatModelTests
{
    [Fact]
    public void InnerModel_ReturnsInjectedModel()
    {
        // Arrange
        var model = new MockChatModel("response");
        var registry = new ToolRegistry();
        var sut = new ToolAwareChatModel(model, registry);

        // Assert
        sut.InnerModel.Should().BeSameAs(model);
    }

    [Fact]
    public void SupportsThinking_WhenRegularModel_ReturnsFalse()
    {
        // Arrange
        var model = new MockChatModel("response");
        var registry = new ToolRegistry();
        var sut = new ToolAwareChatModel(model, registry);

        // Assert
        sut.SupportsThinking.Should().BeFalse();
    }

    [Fact]
    public void SupportsThinking_WhenThinkingModel_ReturnsTrue()
    {
        // Arrange
        var model = new MockThinkingChatModel("thinking", "content");
        var registry = new ToolRegistry();
        var sut = new ToolAwareChatModel(model, registry);

        // Assert
        sut.SupportsThinking.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateWithToolsAsync_WithNoToolCalls_ReturnsOriginalText()
    {
        // Arrange
        var model = new MockChatModel("Hello, how can I help?");
        var registry = new ToolRegistry();
        var sut = new ToolAwareChatModel(model, registry);

        // Act
        var (text, tools) = await sut.GenerateWithToolsAsync("Hi");

        // Assert
        text.Should().Be("Hello, how can I help?");
        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateWithToolsAsync_WithUnknownTool_ReturnsErrorInText()
    {
        // Arrange
        var model = new MockChatModel("Result: [TOOL:unknown_tool arg1]");
        var registry = new ToolRegistry();
        var sut = new ToolAwareChatModel(model, registry);

        // Act
        var (text, tools) = await sut.GenerateWithToolsAsync("test");

        // Assert
        text.Should().Contain("[TOOL-RESULT:unknown_tool] error: tool not found");
        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateWithThinkingAndToolsAsync_WithNonThinkingModel_ReturnsWrappedResponse()
    {
        // Arrange
        var model = new MockChatModel("Some plain response");
        var registry = new ToolRegistry();
        var sut = new ToolAwareChatModel(model, registry);

        // Act
        var (response, tools) = await sut.GenerateWithThinkingAndToolsAsync("test prompt");

        // Assert
        response.Thinking.Should().BeNull();
        response.Content.Should().Be("Some plain response");
        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateWithThinkingAndToolsAsync_WithThinkingModel_ReturnsThinkingResponse()
    {
        // Arrange
        var model = new MockThinkingChatModel("my thinking", "my content");
        var registry = new ToolRegistry();
        var sut = new ToolAwareChatModel(model, registry);

        // Act
        var (response, tools) = await sut.GenerateWithThinkingAndToolsAsync("test prompt");

        // Assert
        response.Thinking.Should().Be("my thinking");
        response.Content.Should().Be("my content");
        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateWithToolsResultAsync_WithNoError_ReturnsSuccess()
    {
        // Arrange
        var model = new MockChatModel("Simple response");
        var registry = new ToolRegistry();
        var sut = new ToolAwareChatModel(model, registry);

        // Act
        var result = await sut.GenerateWithToolsResultAsync("test");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateWithThinkingAndToolsResultAsync_WithNoError_ReturnsSuccess()
    {
        // Arrange
        var model = new MockChatModel("Simple response");
        var registry = new ToolRegistry();
        var sut = new ToolAwareChatModel(model, registry);

        // Act
        var result = await sut.GenerateWithThinkingAndToolsResultAsync("test");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task BeforeInvoke_WhenDenied_SkipsTool()
    {
        // Arrange
        var model = new MockChatModel("[TOOL:test_tool args]");
        var registry = new ToolRegistry();
        registry.Register(new DummyTool("test_tool", "success"));
        var sut = new ToolAwareChatModel(model, registry);
        sut.BeforeInvoke = (_, _, _) => Task.FromResult(false);

        // Act
        var (text, tools) = await sut.GenerateWithToolsAsync("test");

        // Assert
        text.Should().Contain("denied by user");
        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task BeforeInvoke_WhenAllowed_ExecutesTool()
    {
        // Arrange
        var model = new MockChatModel("[TOOL:test_tool args]");
        var registry = new ToolRegistry();
        registry.Register(new DummyTool("test_tool", "tool output"));
        var sut = new ToolAwareChatModel(model, registry);
        sut.BeforeInvoke = (_, _, _) => Task.FromResult(true);

        // Act
        var (text, tools) = await sut.GenerateWithToolsAsync("test");

        // Assert
        text.Should().Contain("[TOOL-RESULT:test_tool] tool output");
        tools.Should().HaveCount(1);
        tools[0].Output.Should().Be("tool output");
    }

    [Fact]
    public async Task AfterInvoke_IsCalled_AfterToolExecution()
    {
        // Arrange
        var model = new MockChatModel("[TOOL:test_tool args]");
        var registry = new ToolRegistry();
        registry.Register(new DummyTool("test_tool", "output"));
        var sut = new ToolAwareChatModel(model, registry);
        string? capturedToolName = null;
        bool capturedSuccess = false;

        sut.AfterInvoke = (name, _, _, _, success) =>
        {
            capturedToolName = name;
            capturedSuccess = success;
        };

        // Act
        await sut.GenerateWithToolsAsync("test");

        // Assert
        capturedToolName.Should().Be("test_tool");
        capturedSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateWithToolsAsync_WithMultipleTools_ExecutesAll()
    {
        // Arrange
        var model = new MockChatModel("First: [TOOL:tool_a x] Second: [TOOL:tool_b y]");
        var registry = new ToolRegistry();
        registry.Register(new DummyTool("tool_a", "result_a"));
        registry.Register(new DummyTool("tool_b", "result_b"));
        var sut = new ToolAwareChatModel(model, registry);

        // Act
        var (text, tools) = await sut.GenerateWithToolsAsync("test");

        // Assert
        tools.Should().HaveCount(2);
        text.Should().Contain("[TOOL-RESULT:tool_a] result_a");
        text.Should().Contain("[TOOL-RESULT:tool_b] result_b");
    }

    /// <summary>
    /// Simple tool implementation for testing.
    /// </summary>
    private sealed class DummyTool : ITool
    {
        private readonly string _output;

        public DummyTool(string name, string output)
        {
            Name = name;
            _output = output;
        }

        public string Name { get; }
        public string Description => "Test tool";

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            return Task.FromResult(Result<string, string>.Success(_output));
        }
    }
}
