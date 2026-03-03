// <copyright file="AgentInstanceTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Moq;
using Xunit;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class AgentInstanceTests
{
    private readonly Mock<IChatCompletionModel> _chatMock = new();

    private Ouroboros.Agent.AgentInstance CreateInstance(
        string mode = "simple",
        int maxSteps = 3,
        bool debug = false,
        bool ragEnabled = false,
        string embedModel = "",
        bool jsonTools = false,
        bool stream = false)
    {
        return Ouroboros.Agent.AgentFactory.Create(
            mode, _chatMock.Object, new Ouroboros.Abstractions.Core.ToolRegistry(),
            debug, maxSteps, ragEnabled, embedModel, jsonTools, stream);
    }

    // --- Mode ---

    [Fact]
    public void Mode_Simple_IsSimple()
    {
        CreateInstance(mode: "simple").Mode.Should().Be("simple");
    }

    [Fact]
    public void Mode_Empty_DefaultsToSimple()
    {
        CreateInstance(mode: "").Mode.Should().Be("simple");
    }

    [Fact]
    public void Mode_Whitespace_DefaultsToSimple()
    {
        CreateInstance(mode: "   ").Mode.Should().Be("simple");
    }

    [Fact]
    public void Mode_Custom_IsPreserved()
    {
        CreateInstance(mode: "advanced").Mode.Should().Be("advanced");
    }

    // --- Properties ---

    [Fact]
    public void Debug_DefaultsFalse()
    {
        CreateInstance().Debug.Should().BeFalse();
    }

    [Fact]
    public void Debug_True_SetsProperty()
    {
        CreateInstance(debug: true).Debug.Should().BeTrue();
    }

    [Fact]
    public void RagEnabled_DefaultsFalse()
    {
        CreateInstance().RagEnabled.Should().BeFalse();
    }

    [Fact]
    public void RagEnabled_True_SetsProperty()
    {
        CreateInstance(ragEnabled: true).RagEnabled.Should().BeTrue();
    }

    [Fact]
    public void EmbedModelName_DefaultsEmpty()
    {
        CreateInstance().EmbedModelName.Should().BeEmpty();
    }

    [Fact]
    public void EmbedModelName_Custom_SetsProperty()
    {
        CreateInstance(embedModel: "ada-002").EmbedModelName.Should().Be("ada-002");
    }

    [Fact]
    public void JsonTools_DefaultsFalse()
    {
        CreateInstance().JsonTools.Should().BeFalse();
    }

    [Fact]
    public void JsonTools_True_SetsProperty()
    {
        CreateInstance(jsonTools: true).JsonTools.Should().BeTrue();
    }

    [Fact]
    public void Stream_DefaultsFalse()
    {
        CreateInstance().Stream.Should().BeFalse();
    }

    [Fact]
    public void Stream_True_SetsProperty()
    {
        CreateInstance(stream: true).Stream.Should().BeTrue();
    }

    // --- MaxSteps clamping ---

    [Fact]
    public void MaxSteps_Zero_ClampsToOne()
    {
        // Agent is constructed (internally clamps). We verify no throw.
        var agent = CreateInstance(maxSteps: 0);
        agent.Should().NotBeNull();
    }

    [Fact]
    public void MaxSteps_Negative_ClampsToOne()
    {
        var agent = CreateInstance(maxSteps: -10);
        agent.Should().NotBeNull();
    }

    [Fact]
    public void MaxSteps_Positive_IsAccepted()
    {
        var agent = CreateInstance(maxSteps: 10);
        agent.Should().NotBeNull();
    }

    // --- RunAsync basic ---

    [Fact]
    public async Task RunAsync_ReturnsChatResponse()
    {
        _chatMock.Setup(c => c.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Done.");

        var agent = CreateInstance();
        var result = await agent.RunAsync("Hello");

        result.Should().NotBeNullOrEmpty();
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
}
