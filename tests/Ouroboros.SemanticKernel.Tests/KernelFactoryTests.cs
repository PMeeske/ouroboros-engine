// <copyright file="KernelFactoryTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Ouroboros.Abstractions.Core;
using Ouroboros.Tools;

namespace Ouroboros.SemanticKernel.Tests;

public sealed class KernelFactoryTests
{
    // ── CreateKernel(IChatCompletionModel) ────────────────────────────────

    [Fact]
    public void CreateKernel_NullChatCompletionModel_ThrowsArgumentNullException()
    {
        var act = () => KernelFactory.CreateKernel((IChatCompletionModel)null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("model");
    }

    [Fact]
    public void CreateKernel_ValidChatCompletionModel_ReturnsKernel()
    {
#pragma warning disable CS0618 // IChatCompletionModel is obsolete
        var mockModel = new Mock<IChatCompletionModel>();
#pragma warning restore CS0618
        mockModel
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("response");

        var kernel = KernelFactory.CreateKernel(mockModel.Object);

        kernel.Should().NotBeNull();
        kernel.Should().BeOfType<Kernel>();
    }

    [Fact]
    public void CreateKernel_WithToolRegistry_RegistersPlugins()
    {
#pragma warning disable CS0618
        var mockModel = new Mock<IChatCompletionModel>();
#pragma warning restore CS0618

        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("TestTool");
        mockTool.Setup(t => t.Description).Returns("A test");

        var tools = new Ouroboros.Tools.ToolRegistry().WithTool(mockTool.Object);

        var kernel = KernelFactory.CreateKernel(mockModel.Object, tools);

        kernel.Should().NotBeNull();
        kernel.Plugins.Should().NotBeEmpty();
        kernel.Plugins.Should().Contain(p => p.Name == "OuroborosTools");
    }

    [Fact]
    public void CreateKernel_WithEmptyToolRegistry_DoesNotRegisterPlugins()
    {
#pragma warning disable CS0618
        var mockModel = new Mock<IChatCompletionModel>();
#pragma warning restore CS0618

        var tools = new Ouroboros.Tools.ToolRegistry();

        var kernel = KernelFactory.CreateKernel(mockModel.Object, tools);

        kernel.Should().NotBeNull();
        kernel.Plugins.Should().BeEmpty();
    }

    [Fact]
    public void CreateKernel_WithNullTools_DoesNotRegisterPlugins()
    {
#pragma warning disable CS0618
        var mockModel = new Mock<IChatCompletionModel>();
#pragma warning restore CS0618

        var kernel = KernelFactory.CreateKernel(mockModel.Object, tools: null);

        kernel.Should().NotBeNull();
        kernel.Plugins.Should().BeEmpty();
    }

    // ── CreateKernel(IChatCompletionModel + IChatClientBridge) ────────────

    [Fact]
    public void CreateKernel_ModelImplementsIChatClientBridge_UsesNativeChatClient()
    {
        var mockChatClient = new Mock<IChatClient>();
        var bridgeMock = new Mock<IChatCompletionModelWithBridge>();
        bridgeMock.Setup(b => b.GetChatClient()).Returns(mockChatClient.Object);

        var kernel = KernelFactory.CreateKernel(bridgeMock.Object);

        kernel.Should().NotBeNull();
        bridgeMock.Verify(b => b.GetChatClient(), Times.Once);
    }

    // ── CreateKernel(IChatClient) ────────────────────────────────────────

    [Fact]
    public void CreateKernel_NullChatClient_ThrowsArgumentNullException()
    {
        var act = () => KernelFactory.CreateKernel((IChatClient)null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("chatClient");
    }

    [Fact]
    public void CreateKernel_ValidChatClient_ReturnsKernel()
    {
        var mockClient = new Mock<IChatClient>();

        var kernel = KernelFactory.CreateKernel(mockClient.Object);

        kernel.Should().NotBeNull();
    }

    // ── CreateKernel(IOuroborosChatClient) ────────────────────────────────

    [Fact]
    public void CreateKernel_NullOuroborosChatClient_ThrowsArgumentNullException()
    {
        var act = () => KernelFactory.CreateKernel((IOuroborosChatClient)null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("client");
    }

    [Fact]
    public void CreateKernel_ValidOuroborosChatClient_ReturnsKernel()
    {
        var mockClient = new Mock<IOuroborosChatClient>();

        var kernel = KernelFactory.CreateKernel(mockClient.Object);

        kernel.Should().NotBeNull();
    }

    [Fact]
    public void CreateKernel_OuroborosChatClient_WithTools_RegistersPlugins()
    {
        var mockClient = new Mock<IOuroborosChatClient>();
        var mockTool = new Mock<ITool>();
        mockTool.Setup(t => t.Name).Returns("Tool1");
        mockTool.Setup(t => t.Description).Returns("desc");

        var tools = new Ouroboros.Tools.ToolRegistry().WithTool(mockTool.Object);

        var kernel = KernelFactory.CreateKernel(mockClient.Object, tools);

        kernel.Plugins.Should().Contain(p => p.Name == "OuroborosTools");
    }

    /// <summary>
    /// Helper interface to combine IChatCompletionModel and IChatClientBridge
    /// for testing the bridge detection path.
    /// </summary>
#pragma warning disable CS0618
    public interface IChatCompletionModelWithBridge : IChatCompletionModel, IChatClientBridge
    {
    }
#pragma warning restore CS0618
}
