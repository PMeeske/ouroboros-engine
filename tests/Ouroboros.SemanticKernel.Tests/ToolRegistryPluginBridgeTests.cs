// <copyright file="ToolRegistryPluginBridgeTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.SemanticKernel;

namespace Ouroboros.SemanticKernel.Tests;

public sealed class ToolRegistryPluginBridgeTests
{
    // ── Null guard tests ─────────────────────────────────────────────────

    [Fact]
    public void ToKernelPlugin_NullRegistry_ThrowsArgumentNullException()
    {
        var act = () => ToolRegistryPluginBridge.ToKernelPlugin(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("registry");
    }

    [Fact]
    public void ToKernelPlugin_NullPluginName_ThrowsArgumentException()
    {
        var registry = new ToolRegistry();
        var act = () => ToolRegistryPluginBridge.ToKernelPlugin(registry, null!);
        act.Should().Throw<ArgumentException>().WithParameterName("pluginName");
    }

    [Fact]
    public void ToKernelPlugin_WhitespacePluginName_ThrowsArgumentException()
    {
        var registry = new ToolRegistry();
        var act = () => ToolRegistryPluginBridge.ToKernelPlugin(registry, "   ");
        act.Should().Throw<ArgumentException>().WithParameterName("pluginName");
    }

    // ── Empty registry ───────────────────────────────────────────────────

    [Fact]
    public void ToKernelPlugin_EmptyRegistry_ReturnsEmptyPlugin()
    {
        var registry = new ToolRegistry();

        var plugin = ToolRegistryPluginBridge.ToKernelPlugin(registry);

        plugin.Should().NotBeNull();
        plugin.Name.Should().Be("OuroborosTools");
        plugin.Should().BeEmpty();
    }

    // ── Default plugin name ──────────────────────────────────────────────

    [Fact]
    public void ToKernelPlugin_DefaultPluginName_IsOuroborosTools()
    {
        var registry = new ToolRegistry();

        var plugin = ToolRegistryPluginBridge.ToKernelPlugin(registry);

        plugin.Name.Should().Be("OuroborosTools");
    }

    // ── Custom plugin name ───────────────────────────────────────────────

    [Fact]
    public void ToKernelPlugin_CustomPluginName_UsesGivenName()
    {
        var registry = new ToolRegistry();

        var plugin = ToolRegistryPluginBridge.ToKernelPlugin(registry, "CustomTools");

        plugin.Name.Should().Be("CustomTools");
    }

    // ── Single tool registration ─────────────────────────────────────────

    [Fact]
    public void ToKernelPlugin_SingleTool_CreatesOneFunction()
    {
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("MyTool");
        mockTool.Setup(t => t.Description).Returns("A test tool");

        var registry = new ToolRegistry().WithTool(mockTool.Object);

        var plugin = ToolRegistryPluginBridge.ToKernelPlugin(registry);

        plugin.Should().HaveCount(1);
        plugin.First().Name.Should().Be("MyTool");
        plugin.First().Description.Should().Be("A test tool");
    }

    // ── Multiple tools ───────────────────────────────────────────────────

    [Fact]
    public void ToKernelPlugin_MultipleTools_CreatesMultipleFunctions()
    {
        var tool1 = new Mock<ITool>();
        tool1.Setup(t => t.Name).Returns("ToolA");
        tool1.Setup(t => t.Description).Returns("First tool");

        var tool2 = new Mock<ITool>();
        tool2.Setup(t => t.Name).Returns("ToolB");
        tool2.Setup(t => t.Description).Returns("Second tool");

        var registry = new ToolRegistry()
            .WithTool(tool1.Object)
            .WithTool(tool2.Object);

        var plugin = ToolRegistryPluginBridge.ToKernelPlugin(registry);

        plugin.Should().HaveCount(2);
        plugin.Select(f => f.Name).Should().Contain("ToolA").And.Contain("ToolB");
    }

    // ── Function invocation (success) ────────────────────────────────────

    [Fact]
    public async Task ToKernelPlugin_InvokeFunction_SuccessResult_ReturnsValue()
    {
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("EchoTool");
        mockTool.Setup(t => t.Description).Returns("Echoes input");
        mockTool
            .Setup(t => t.InvokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ouroboros.Abstractions.Monads.Result<string, string>.Success("echoed: hello"));

        var registry = new ToolRegistry().WithTool(mockTool.Object);
        var plugin = ToolRegistryPluginBridge.ToKernelPlugin(registry);
        var function = plugin.First();

        var kernel = Kernel.CreateBuilder().Build();
        var result = await function.InvokeAsync(kernel, new KernelArguments
        {
            ["input"] = "hello",
        });

        result.GetValue<string>().Should().Be("echoed: hello");
    }

    // ── Function invocation (failure) ────────────────────────────────────

    [Fact]
    public async Task ToKernelPlugin_InvokeFunction_FailureResult_ReturnsErrorString()
    {
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("FailTool");
        mockTool.Setup(t => t.Description).Returns("Fails always");
        mockTool
            .Setup(t => t.InvokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ouroboros.Abstractions.Monads.Result<string, string>.Failure("something went wrong"));

        var registry = new ToolRegistry().WithTool(mockTool.Object);
        var plugin = ToolRegistryPluginBridge.ToKernelPlugin(registry);
        var function = plugin.First();

        var kernel = Kernel.CreateBuilder().Build();
        var result = await function.InvokeAsync(kernel, new KernelArguments
        {
            ["input"] = "test",
        });

        result.GetValue<string>().Should().Contain("Error: something went wrong");
    }

    // ── Name sanitization ────────────────────────────────────────────────

    [Fact]
    public void ToKernelPlugin_ToolNameWithSpecialChars_SanitizedToValidIdentifier()
    {
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("my-tool.v2");
        mockTool.Setup(t => t.Description).Returns("Tool with special chars");

        var registry = new ToolRegistry().WithTool(mockTool.Object);
        var plugin = ToolRegistryPluginBridge.ToKernelPlugin(registry);

        var function = plugin.First();
        // Hyphens and dots should be replaced with underscores
        function.Name.Should().Be("my_tool_v2");
    }

    [Fact]
    public void ToKernelPlugin_ToolNameStartingWithDigit_PrefixedWithTool()
    {
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("42Tool");
        mockTool.Setup(t => t.Description).Returns("Numeric start");

        var registry = new ToolRegistry().WithTool(mockTool.Object);
        var plugin = ToolRegistryPluginBridge.ToKernelPlugin(registry);

        var function = plugin.First();
        function.Name.Should().Be("Tool_42Tool");
    }

    [Fact]
    public void ToKernelPlugin_ToolNameWithUnderscores_PreservedAsIs()
    {
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("my_tool_name");
        mockTool.Setup(t => t.Description).Returns("Underscored name");

        var registry = new ToolRegistry().WithTool(mockTool.Object);
        var plugin = ToolRegistryPluginBridge.ToKernelPlugin(registry);

        plugin.First().Name.Should().Be("my_tool_name");
    }
}
