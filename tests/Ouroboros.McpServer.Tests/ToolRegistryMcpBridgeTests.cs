using FluentAssertions;
using Moq;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Tools;
using Xunit;

namespace Ouroboros.McpServer.Tests;

[Trait("Category", "Unit")]
public class ToolRegistryMcpBridgeTests
{
    private static ITool CreateMockTool(
        string name,
        string description = "desc",
        string? jsonSchema = null)
    {
        var mock = new Mock<ITool>();
        mock.Setup(t => t.Name).Returns(name);
        mock.Setup(t => t.Description).Returns(description);
        mock.Setup(t => t.JsonSchema).Returns(jsonSchema);
        return mock.Object;
    }

    private static ITool CreateInvokableTool(
        string name,
        Result<string, string> result)
    {
        var mock = new Mock<ITool>();
        mock.Setup(t => t.Name).Returns(name);
        mock.Setup(t => t.Description).Returns("test");
        mock.Setup(t => t.JsonSchema).Returns((string?)null);
        mock.Setup(t => t.InvokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return mock.Object;
    }

    // --- ToMcpTools ---

    [Fact]
    public void ToMcpTools_NullRegistry_ThrowsArgumentNullException()
    {
        var act = () => ToolRegistryMcpBridge.ToMcpTools(null!);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("registry");
    }

    [Fact]
    public void ToMcpTools_EmptyRegistry_ReturnsEmptyList()
    {
        var registry = new ToolRegistry();

        var result = ToolRegistryMcpBridge.ToMcpTools(registry);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ToMcpTools_SingleTool_ReturnsOneDefinition()
    {
        var tool = CreateMockTool("calculator", "Does math");
        var registry = new ToolRegistry().WithTool(tool);

        var result = ToolRegistryMcpBridge.ToMcpTools(registry);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("calculator");
        result[0].Description.Should().Be("Does math");
    }

    [Fact]
    public void ToMcpTools_MultipleTools_ReturnsAllDefinitions()
    {
        var registry = new ToolRegistry()
            .WithTool(CreateMockTool("tool1"))
            .WithTool(CreateMockTool("tool2"))
            .WithTool(CreateMockTool("tool3"));

        var result = ToolRegistryMcpBridge.ToMcpTools(registry);

        result.Should().HaveCount(3);
    }

    [Fact]
    public void ToMcpTools_WithValidJsonSchema_ParsesInputSchema()
    {
        var tool = CreateMockTool("tool", "desc", """{"type":"object","properties":{"x":{"type":"number"}}}""");
        var registry = new ToolRegistry().WithTool(tool);

        var result = ToolRegistryMcpBridge.ToMcpTools(registry);

        result[0].InputSchema.ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Undefined);
        result[0].InputSchema.GetProperty("type").GetString().Should().Be("object");
    }

    [Fact]
    public void ToMcpTools_WithNullJsonSchema_SetsInputSchemaNull()
    {
        var tool = CreateMockTool("tool", "desc", null);
        var registry = new ToolRegistry().WithTool(tool);

        var result = ToolRegistryMcpBridge.ToMcpTools(registry);

        result[0].InputSchema.Should().BeNull();
    }

    [Fact]
    public void ToMcpTools_WithInvalidJsonSchema_SetsInputSchemaNull()
    {
        var tool = CreateMockTool("tool", "desc", "not-json{{{");
        var registry = new ToolRegistry().WithTool(tool);

        var result = ToolRegistryMcpBridge.ToMcpTools(registry);

        result[0].InputSchema.Should().BeNull();
    }

    [Fact]
    public void ToMcpTools_WithEmptyJsonSchema_SetsInputSchemaNull()
    {
        var tool = CreateMockTool("tool", "desc", "");
        var registry = new ToolRegistry().WithTool(tool);

        var result = ToolRegistryMcpBridge.ToMcpTools(registry);

        result[0].InputSchema.Should().BeNull();
    }

    [Fact]
    public void ToMcpTools_WithWhitespaceJsonSchema_SetsInputSchemaNull()
    {
        var tool = CreateMockTool("tool", "desc", "   ");
        var registry = new ToolRegistry().WithTool(tool);

        var result = ToolRegistryMcpBridge.ToMcpTools(registry);

        result[0].InputSchema.Should().BeNull();
    }

    // --- Filter ---

    [Fact]
    public void ToMcpTools_WithFilter_ReturnsOnlyMatchingTools()
    {
        var registry = new ToolRegistry()
            .WithTool(CreateMockTool("alpha"))
            .WithTool(CreateMockTool("beta"))
            .WithTool(CreateMockTool("gamma"));

        var result = ToolRegistryMcpBridge.ToMcpTools(registry, ["alpha", "gamma"]);

        result.Should().HaveCount(2);
        result.Select(t => t.Name).Should().BeEquivalentTo(new[] { "alpha", "gamma" });
    }

    [Fact]
    public void ToMcpTools_WithFilter_IsCaseInsensitive()
    {
        var registry = new ToolRegistry()
            .WithTool(CreateMockTool("MyTool"));

        var result = ToolRegistryMcpBridge.ToMcpTools(registry, ["mytool"]);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void ToMcpTools_WithEmptyFilter_ReturnsAllTools()
    {
        var registry = new ToolRegistry()
            .WithTool(CreateMockTool("tool1"))
            .WithTool(CreateMockTool("tool2"));

        var result = ToolRegistryMcpBridge.ToMcpTools(registry, []);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void ToMcpTools_WithNullFilter_ReturnsAllTools()
    {
        var registry = new ToolRegistry()
            .WithTool(CreateMockTool("tool1"))
            .WithTool(CreateMockTool("tool2"));

        var result = ToolRegistryMcpBridge.ToMcpTools(registry, null);

        result.Should().HaveCount(2);
    }

    // --- InvokeToolAsync ---

    [Fact]
    public async Task InvokeToolAsync_NullRegistry_ThrowsArgumentNullException()
    {
        var act = () => ToolRegistryMcpBridge.InvokeToolAsync(null!, "tool", null);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .Where(e => e.ParamName == "registry");
    }

    [Fact]
    public async Task InvokeToolAsync_NullToolName_ThrowsArgumentNullException()
    {
        var registry = new ToolRegistry();

        var act = () => ToolRegistryMcpBridge.InvokeToolAsync(registry, null!, null);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .Where(e => e.ParamName == "toolName");
    }

    [Fact]
    public async Task InvokeToolAsync_ToolNotFound_ReturnsError()
    {
        var registry = new ToolRegistry();

        var result = await ToolRegistryMcpBridge.InvokeToolAsync(registry, "missing", null);

        result.IsError.Should().BeTrue();
        result.Content.Should().Contain("missing");
        result.Content.Should().Contain("not found");
    }

    [Fact]
    public async Task InvokeToolAsync_ToolReturnsSuccess_ReturnsSuccessResult()
    {
        var tool = CreateInvokableTool("calc", Result<string, string>.Success("42"));
        var registry = new ToolRegistry().WithTool(tool);

        var result = await ToolRegistryMcpBridge.InvokeToolAsync(registry, "calc", """{"x":1}""");

        result.IsError.Should().BeFalse();
        result.Content.Should().Be("42");
    }

    [Fact]
    public async Task InvokeToolAsync_ToolReturnsFailure_ReturnsErrorResult()
    {
        var tool = CreateInvokableTool("calc", Result<string, string>.Failure("division by zero"));
        var registry = new ToolRegistry().WithTool(tool);

        var result = await ToolRegistryMcpBridge.InvokeToolAsync(registry, "calc", """{"x":0}""");

        result.IsError.Should().BeTrue();
        result.Content.Should().Be("division by zero");
    }

    [Fact]
    public async Task InvokeToolAsync_NullArguments_PassesEmptyString()
    {
        var mock = new Mock<ITool>();
        mock.Setup(t => t.Name).Returns("echo");
        mock.Setup(t => t.Description).Returns("test");
        mock.Setup(t => t.InvokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success("ok"));
        var registry = new ToolRegistry().WithTool(mock.Object);

        await ToolRegistryMcpBridge.InvokeToolAsync(registry, "echo", null);

        mock.Verify(t => t.InvokeAsync(string.Empty, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvokeToolAsync_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var mock = new Mock<ITool>();
        mock.Setup(t => t.Name).Returns("echo");
        mock.Setup(t => t.Description).Returns("test");
        mock.Setup(t => t.InvokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success("ok"));
        var registry = new ToolRegistry().WithTool(mock.Object);

        await ToolRegistryMcpBridge.InvokeToolAsync(registry, "echo", "input", cts.Token);

        mock.Verify(t => t.InvokeAsync("input", cts.Token), Times.Once);
    }
}
