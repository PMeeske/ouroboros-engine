// <copyright file="AgentFactoryTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Moq;
using Ouroboros.Abstractions.Core;
using Ouroboros.Agent;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class AgentFactoryTests
{
    private readonly Mock<IChatCompletionModel> _chatModelMock = new();
    private readonly ToolRegistry _tools = new();

    [Fact]
    public void Create_ReturnsAgentInstance()
    {
        // Act
        var agent = AgentFactory.Create(
            "simple", _chatModelMock.Object, _tools,
            debug: false, maxSteps: 5, ragEnabled: false,
            embedModelName: "embed", jsonTools: false, stream: false);

        // Assert
        agent.Should().NotBeNull();
        agent.Should().BeOfType<AgentInstance>();
    }

    [Fact]
    public void Create_SetsMode()
    {
        // Act
        var agent = AgentFactory.Create(
            "plan", _chatModelMock.Object, _tools,
            debug: false, maxSteps: 5, ragEnabled: false,
            embedModelName: "", jsonTools: false, stream: false);

        // Assert
        agent.Mode.Should().Be("plan");
    }

    [Fact]
    public void Create_SetsDebugFlag()
    {
        // Act
        var agent = AgentFactory.Create(
            "simple", _chatModelMock.Object, _tools,
            debug: true, maxSteps: 5, ragEnabled: false,
            embedModelName: "", jsonTools: false, stream: false);

        // Assert
        agent.Debug.Should().BeTrue();
    }

    [Fact]
    public void Create_SetsRagEnabled()
    {
        // Act
        var agent = AgentFactory.Create(
            "simple", _chatModelMock.Object, _tools,
            debug: false, maxSteps: 5, ragEnabled: true,
            embedModelName: "all-minilm", jsonTools: false, stream: false);

        // Assert
        agent.RagEnabled.Should().BeTrue();
        agent.EmbedModelName.Should().Be("all-minilm");
    }

    [Fact]
    public void Create_SetsJsonToolsAndStream()
    {
        // Act
        var agent = AgentFactory.Create(
            "simple", _chatModelMock.Object, _tools,
            debug: false, maxSteps: 5, ragEnabled: false,
            embedModelName: "", jsonTools: true, stream: true);

        // Assert
        agent.JsonTools.Should().BeTrue();
        agent.Stream.Should().BeTrue();
    }

    [Fact]
    public void Create_WithEmptyMode_DefaultsToSimple()
    {
        // Act
        var agent = AgentFactory.Create(
            "", _chatModelMock.Object, _tools,
            debug: false, maxSteps: 1, ragEnabled: false,
            embedModelName: "", jsonTools: false, stream: false);

        // Assert
        agent.Mode.Should().Be("simple");
    }

    [Fact]
    public void Create_WithWhitespaceMode_DefaultsToSimple()
    {
        // Act
        var agent = AgentFactory.Create(
            "   ", _chatModelMock.Object, _tools,
            debug: false, maxSteps: 1, ragEnabled: false,
            embedModelName: "", jsonTools: false, stream: false);

        // Assert
        agent.Mode.Should().Be("simple");
    }
}
