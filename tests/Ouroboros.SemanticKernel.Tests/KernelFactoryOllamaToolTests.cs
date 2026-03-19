using Microsoft.SemanticKernel;
using Ouroboros.Providers;
using Ouroboros.Tools;

namespace Ouroboros.SemanticKernel.Tests;

[Trait("Category", "Unit")]
public sealed class KernelFactoryOllamaToolTests
{
    [Fact]
    public void CreateKernel_WithOllamaToolAdapter_ReturnsKernel()
    {
        var registry = new ToolRegistry();
        var parser = new McpToolCallParser();
        using var adapter = new OllamaToolChatAdapter(
            "http://localhost:11434",
            "mistral:latest",
            registry,
            parser);

        Kernel kernel = KernelFactory.CreateKernel(adapter, registry);

        kernel.Should().NotBeNull();
    }

    [Fact]
    public void CreateKernel_WithOllamaToolAdapter_RegistersToolPlugin()
    {
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("test_tool");
        mockTool.Setup(t => t.Description).Returns("A test tool");

        var registry = new ToolRegistry().WithTool(mockTool.Object);
        var parser = new McpToolCallParser();
        using var adapter = new OllamaToolChatAdapter(
            "http://localhost:11434",
            "mistral:latest",
            registry,
            parser);

        Kernel kernel = KernelFactory.CreateKernel(adapter, registry);

        kernel.Plugins.Should().Contain(p => p.Name == "OuroborosTools");
    }

    [Fact]
    public void CreateKernel_NullAdapter_ThrowsArgumentNullException()
    {
        var act = () => KernelFactory.CreateKernel(
            (OllamaToolChatAdapter)null!,
            new ToolRegistry());

        act.Should().Throw<ArgumentNullException>().WithParameterName("adapter");
    }

    [Fact]
    public void CreateKernel_NullTools_ThrowsArgumentNullException()
    {
        using var adapter = new OllamaToolChatAdapter(
            "http://localhost:11434",
            "model",
            new ToolRegistry(),
            new McpToolCallParser());

        var act = () => KernelFactory.CreateKernel(adapter, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("tools");
    }

    [Fact]
    public void CreateKernel_WithAdditionalPlugins_IncludesAll()
    {
        var registry = new ToolRegistry();
        var parser = new McpToolCallParser();
        using var adapter = new OllamaToolChatAdapter(
            "http://localhost:11434",
            "mistral:latest",
            registry,
            parser);

        var customPlugin = KernelPluginFactory.CreateFromFunctions("CustomPlugin");

        Kernel kernel = KernelFactory.CreateKernel(
            adapter,
            registry,
            additionalPlugins: [customPlugin]);

        kernel.Plugins.Should().Contain(p => p.Name == "CustomPlugin");
    }
}
