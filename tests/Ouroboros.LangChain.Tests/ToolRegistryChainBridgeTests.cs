using FluentAssertions;
using LangChain.Providers;
using Moq;
using Ouroboros.Abstractions.Monads;
using Ouroboros.LangChainBridge;
using Ouroboros.Tools;
using Xunit;

namespace Ouroboros.LangChainBridge.Tests;

[Trait("Category", "Unit")]
public class ToolRegistryChainBridgeTests
{
    private static ITool CreateMockTool(
        string name,
        string description = "desc",
        Result<string, string>? invokeResult = null)
    {
        var mock = new Mock<ITool>();
        mock.Setup(t => t.Name).Returns(name);
        mock.Setup(t => t.Description).Returns(description);
        mock.Setup(t => t.JsonSchema).Returns((string?)null);
        mock.Setup(t => t.InvokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invokeResult ?? Result<string, string>.Success("ok"));
        return mock.Object;
    }

    // --- Null guards ---

    [Fact]
    public void RegisterTools_NullChatModel_ThrowsArgumentNullException()
    {
        var registry = new ToolRegistry();

        var act = () => ToolRegistryChainBridge.RegisterTools(null!, registry);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("chatModel");
    }

    [Fact]
    public void RegisterTools_NullRegistry_ThrowsArgumentNullException()
    {
        var mockModel = new Mock<IChatModel>();

        var act = () => ToolRegistryChainBridge.RegisterTools(mockModel.Object, null!);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("registry");
    }

    // --- Empty registry ---

    [Fact]
    public void RegisterTools_EmptyRegistry_DoesNotCallAddGlobalTools()
    {
        var mockModel = new Mock<IChatModel>();
        var registry = new ToolRegistry();

        ToolRegistryChainBridge.RegisterTools(mockModel.Object, registry);

        mockModel.Verify(
            m => m.AddGlobalTools(
                It.IsAny<ICollection<CSharpToJsonSchema.Tool>>(),
                It.IsAny<IReadOnlyDictionary<string, Func<string, CancellationToken, Task<string>>>>()),
            Times.Never);
    }

    // --- Registration ---

    [Fact]
    public void RegisterTools_SingleTool_CallsAddGlobalToolsOnce()
    {
        var mockModel = new Mock<IChatModel>();
        var registry = new ToolRegistry().WithTool(CreateMockTool("calc"));

        ToolRegistryChainBridge.RegisterTools(mockModel.Object, registry);

        mockModel.Verify(
            m => m.AddGlobalTools(
                It.IsAny<ICollection<CSharpToJsonSchema.Tool>>(),
                It.IsAny<IReadOnlyDictionary<string, Func<string, CancellationToken, Task<string>>>>()),
            Times.Once);
    }

    [Fact]
    public void RegisterTools_MultipleTools_RegistersAllInSingleCall()
    {
        var mockModel = new Mock<IChatModel>();
        var registry = new ToolRegistry()
            .WithTool(CreateMockTool("tool1"))
            .WithTool(CreateMockTool("tool2"))
            .WithTool(CreateMockTool("tool3"));

        ToolRegistryChainBridge.RegisterTools(mockModel.Object, registry);

        mockModel.Verify(
            m => m.AddGlobalTools(
                It.IsAny<ICollection<CSharpToJsonSchema.Tool>>(),
                It.Is<IReadOnlyDictionary<string, Func<string, CancellationToken, Task<string>>>>(
                    d => d.Count == 3)),
            Times.Once);
    }

    // --- Filter ---

    [Fact]
    public void RegisterTools_WithFilter_RegistersOnlyMatchingTools()
    {
        var mockModel = new Mock<IChatModel>();
        var registry = new ToolRegistry()
            .WithTool(CreateMockTool("include-me"))
            .WithTool(CreateMockTool("exclude-me"));

        ToolRegistryChainBridge.RegisterTools(
            mockModel.Object,
            registry,
            name => name == "include-me");

        mockModel.Verify(
            m => m.AddGlobalTools(
                It.IsAny<ICollection<CSharpToJsonSchema.Tool>>(),
                It.Is<IReadOnlyDictionary<string, Func<string, CancellationToken, Task<string>>>>(
                    d => d.Count == 1 && d.ContainsKey("include-me"))),
            Times.Once);
    }

    [Fact]
    public void RegisterTools_FilterExcludesAll_DoesNotCallAddGlobalTools()
    {
        var mockModel = new Mock<IChatModel>();
        var registry = new ToolRegistry()
            .WithTool(CreateMockTool("tool1"));

        ToolRegistryChainBridge.RegisterTools(
            mockModel.Object,
            registry,
            _ => false);

        mockModel.Verify(
            m => m.AddGlobalTools(
                It.IsAny<ICollection<CSharpToJsonSchema.Tool>>(),
                It.IsAny<IReadOnlyDictionary<string, Func<string, CancellationToken, Task<string>>>>()),
            Times.Never);
    }

    [Fact]
    public void RegisterTools_NullFilter_RegistersAllTools()
    {
        var mockModel = new Mock<IChatModel>();
        var registry = new ToolRegistry()
            .WithTool(CreateMockTool("tool1"))
            .WithTool(CreateMockTool("tool2"));

        ToolRegistryChainBridge.RegisterTools(mockModel.Object, registry, null);

        mockModel.Verify(
            m => m.AddGlobalTools(
                It.IsAny<ICollection<CSharpToJsonSchema.Tool>>(),
                It.Is<IReadOnlyDictionary<string, Func<string, CancellationToken, Task<string>>>>(
                    d => d.Count == 2)),
            Times.Once);
    }

    // --- Callback behavior ---

    [Fact]
    public async Task RegisterTools_CallbackOnSuccess_ReturnsToolOutput()
    {
        Dictionary<string, Func<string, CancellationToken, Task<string>>>? captured = null;
        var mockModel = new Mock<IChatModel>();
        mockModel.Setup(m => m.AddGlobalTools(
                It.IsAny<ICollection<CSharpToJsonSchema.Tool>>(),
                It.IsAny<IReadOnlyDictionary<string, Func<string, CancellationToken, Task<string>>>>()))
            .Callback<ICollection<CSharpToJsonSchema.Tool>, IReadOnlyDictionary<string, Func<string, CancellationToken, Task<string>>>>(
                (_, callbacks) => captured = new Dictionary<string, Func<string, CancellationToken, Task<string>>>(callbacks));

        var tool = CreateMockTool("echo", invokeResult: Result<string, string>.Success("hello world"));
        var registry = new ToolRegistry().WithTool(tool);

        ToolRegistryChainBridge.RegisterTools(mockModel.Object, registry);

        captured.Should().NotBeNull();
        captured.Should().ContainKey("echo");
        var result = await captured!["echo"]("input", CancellationToken.None);
        result.Should().Be("hello world");
    }

    [Fact]
    public async Task RegisterTools_CallbackOnError_ReturnsErrorPrefixedMessage()
    {
        Dictionary<string, Func<string, CancellationToken, Task<string>>>? captured = null;
        var mockModel = new Mock<IChatModel>();
        mockModel.Setup(m => m.AddGlobalTools(
                It.IsAny<ICollection<CSharpToJsonSchema.Tool>>(),
                It.IsAny<IReadOnlyDictionary<string, Func<string, CancellationToken, Task<string>>>>()))
            .Callback<ICollection<CSharpToJsonSchema.Tool>, IReadOnlyDictionary<string, Func<string, CancellationToken, Task<string>>>>(
                (_, callbacks) => captured = new Dictionary<string, Func<string, CancellationToken, Task<string>>>(callbacks));

        var tool = CreateMockTool("fail", invokeResult: Result<string, string>.Failure("bad input"));
        var registry = new ToolRegistry().WithTool(tool);

        ToolRegistryChainBridge.RegisterTools(mockModel.Object, registry);

        var result = await captured!["fail"]("input", CancellationToken.None);
        result.Should().Be("Error: bad input");
    }
}
