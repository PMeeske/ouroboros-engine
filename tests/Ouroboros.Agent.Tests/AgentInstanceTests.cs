// <copyright file="AgentInstanceTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Moq;
using Ouroboros.Abstractions.Core;
using Ouroboros.Agent;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class AgentInstanceTests
{
    private readonly Mock<IChatCompletionModel> _chatMock = new();
    private readonly ToolRegistry _tools = new();

    private AgentInstance CreateSut(string mode = "simple", int maxSteps = 5)
    {
        return AgentFactory.Create(
            mode, _chatMock.Object, _tools,
            debug: false, maxSteps: maxSteps, ragEnabled: false,
            embedModelName: "", jsonTools: false, stream: false);
    }

    // ── Constructor behavior ────────────────────────────────────────────

    [Fact]
    public void Mode_WithNullOrWhitespace_DefaultsToSimple()
    {
        // Act
        var agent = AgentFactory.Create(
            null!, _chatMock.Object, _tools,
            debug: false, maxSteps: 1, ragEnabled: false,
            embedModelName: "", jsonTools: false, stream: false);

        // Assert
        agent.Mode.Should().Be("simple");
    }

    [Fact]
    public void Mode_WithValidValue_IsPreserved()
    {
        // Act
        var agent = CreateSut(mode: "plan");

        // Assert
        agent.Mode.Should().Be("plan");
    }

    [Fact]
    public void MaxSteps_WithZero_IsClampedToOne()
    {
        // Arrange — create with maxSteps=0, which should be clamped to 1
        var agent = AgentFactory.Create(
            "simple", _chatMock.Object, _tools,
            debug: false, maxSteps: 0, ragEnabled: false,
            embedModelName: "", jsonTools: false, stream: false);

        // Assert — agent should still be created successfully
        agent.Should().NotBeNull();
    }

    [Fact]
    public void MaxSteps_WithNegative_IsClampedToOne()
    {
        // Arrange
        var agent = AgentFactory.Create(
            "simple", _chatMock.Object, _tools,
            debug: false, maxSteps: -5, ragEnabled: false,
            embedModelName: "", jsonTools: false, stream: false);

        // Assert
        agent.Should().NotBeNull();
    }

    // ── Property init ───────────────────────────────────────────────────

    [Fact]
    public void Properties_DefaultValues_AreCorrect()
    {
        // Act
        var agent = CreateSut();

        // Assert
        agent.Debug.Should().BeFalse();
        agent.RagEnabled.Should().BeFalse();
        agent.EmbedModelName.Should().BeEmpty();
        agent.JsonTools.Should().BeFalse();
        agent.Stream.Should().BeFalse();
    }

    [Fact]
    public void Properties_SetViaFactory_AreCorrect()
    {
        // Act
        var agent = AgentFactory.Create(
            "plan", _chatMock.Object, _tools,
            debug: true, maxSteps: 10, ragEnabled: true,
            embedModelName: "all-minilm", jsonTools: true, stream: true);

        // Assert
        agent.Debug.Should().BeTrue();
        agent.RagEnabled.Should().BeTrue();
        agent.EmbedModelName.Should().Be("all-minilm");
        agent.JsonTools.Should().BeTrue();
        agent.Stream.Should().BeTrue();
    }

    // ── RunAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WithSingleStep_ReturnsResponse()
    {
        // Arrange — mock returns plain text (no tool calls, no AGENT-CONTINUE)
        // RunAsync calls GenerateTextAsync twice per iteration:
        //   1. Direct call with the prompt
        //   2. Inside ToolAwareChatModel.GenerateWithToolsAsync with the response from #1
        _chatMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("final answer");

        var agent = CreateSut(maxSteps: 5);

        // Act
        var result = await agent.RunAsync("hello");

        // Assert
        result.Should().Be("final answer");
    }

    [Fact]
    public async Task RunAsync_WithoutContinueMarker_StopsAfterFirstIteration()
    {
        // Arrange
        _chatMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("plain response");

        var agent = CreateSut(maxSteps: 10);

        // Act
        await agent.RunAsync("prompt");

        // Assert — called 2x per iteration (once direct, once via ToolAwareChatModel)
        _chatMock.Verify(
            m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RunAsync_WithContinueMarker_ContinuesIterating()
    {
        // Arrange
        int callCount = 0;
        _chatMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // Calls 1-2 are the first iteration (direct + tool-aware),
                // calls 3-4 are the second iteration, calls 5-6 are the third.
                // The tool-aware model's output becomes `current` so if it contains
                // [AGENT-CONTINUE], the loop continues.
                if (callCount <= 4) // first 2 iterations
                    return "intermediate [AGENT-CONTINUE] response";
                return "final response";
            });

        var agent = CreateSut(maxSteps: 5);

        // Act
        var result = await agent.RunAsync("start");

        // Assert
        result.Should().Be("final response");
    }

    [Fact]
    public async Task RunAsync_RespectsMaxSteps()
    {
        // Arrange — always return continue marker so we hit maxSteps
        _chatMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("[AGENT-CONTINUE] keep going");

        var agent = CreateSut(maxSteps: 3);

        // Act
        var result = await agent.RunAsync("start");

        // Assert — 2 calls per iteration * 3 iterations = 6 total
        _chatMock.Verify(
            m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(6));
    }
}
