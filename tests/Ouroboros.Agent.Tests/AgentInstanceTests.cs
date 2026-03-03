// <copyright file="AgentInstanceTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Moq;
using Xunit;
using Ouroboros.Abstractions.Core;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class AgentInstanceTests
{
    private readonly Mock<IChatCompletionModel> _chatMock = new();

    private Agent.AgentInstance CreateInstance(int maxSteps = 3)
    {
        return Agent.AgentFactory.Create(
            "simple", _chatMock.Object, new Agent.ToolRegistry(),
            debug: false, maxSteps: maxSteps, ragEnabled: false,
            embedModelName: "", jsonTools: false, stream: false);
    }

    [Fact]
    public async Task RunAsync_ReturnsChatResponse()
    {
        _chatMock.Setup(c => c.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Hello, world!");

        var agent = CreateInstance();
        var result = await agent.RunAsync("Hello");

        result.Should().Contain("Hello, world!");
    }

    [Fact]
    public async Task RunAsync_CancelledToken_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _chatMock.Setup(c => c.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var agent = CreateInstance();

        Func<Task> act = () => agent.RunAsync("test", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunAsync_NoContinueMarker_ReturnsSingleIteration()
    {
        _chatMock.Setup(c => c.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Done");

        var agent = CreateInstance(maxSteps: 5);
        var result = await agent.RunAsync("test");

        result.Should().Be("Done");
        // Should have been called once for chat + once via ToolAwareChatModel inner call
        _chatMock.Verify(c => c.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
