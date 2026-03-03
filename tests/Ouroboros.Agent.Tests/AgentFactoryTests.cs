// <copyright file="AgentFactoryTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Moq;
using Xunit;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class AgentFactoryTests
{
    private readonly Mock<IChatCompletionModel> _chatMock = new();

    private Ouroboros.Agent.AgentInstance CreateInstance(
        string mode = "simple",
        bool debug = false,
        int maxSteps = 3,
        bool ragEnabled = false,
        string embedModel = "",
        bool jsonTools = false,
        bool stream = false)
    {
        return Ouroboros.Agent.AgentFactory.Create(
            mode, _chatMock.Object, new Ouroboros.Abstractions.Core.ToolRegistry(),
            debug, maxSteps, ragEnabled, embedModel, jsonTools, stream);
    }

    [Fact]
    public void Create_SimpleMode_SetsMode()
    {
        CreateInstance(mode: "simple").Mode.Should().Be("simple");
    }

    [Fact]
    public void Create_NullMode_DefaultsToSimple()
    {
        var agent = Ouroboros.Agent.AgentFactory.Create(
            null!, _chatMock.Object, new Ouroboros.Abstractions.Core.ToolRegistry(),
            false, 3, false, "", false, false);
        agent.Mode.Should().Be("simple");
    }

    [Fact]
    public void Create_WhitespaceMode_DefaultsToSimple()
    {
        CreateInstance(mode: "  ").Mode.Should().Be("simple");
    }

    [Fact]
    public void Create_CustomMode_SetsMode()
    {
        CreateInstance(mode: "planner").Mode.Should().Be("planner");
    }

    [Fact]
    public void Create_Debug_True()
    {
        CreateInstance(debug: true).Debug.Should().BeTrue();
    }

    [Fact]
    public void Create_Debug_False()
    {
        CreateInstance(debug: false).Debug.Should().BeFalse();
    }

    [Fact]
    public void Create_RagEnabled_True()
    {
        CreateInstance(ragEnabled: true).RagEnabled.Should().BeTrue();
    }

    [Fact]
    public void Create_EmbedModelName_Set()
    {
        CreateInstance(embedModel: "text-embedding-3-small").EmbedModelName
            .Should().Be("text-embedding-3-small");
    }

    [Fact]
    public void Create_JsonTools_True()
    {
        CreateInstance(jsonTools: true).JsonTools.Should().BeTrue();
    }

    [Fact]
    public void Create_Stream_True()
    {
        CreateInstance(stream: true).Stream.Should().BeTrue();
    }

    [Fact]
    public void Create_MaxStepsZero_ClampsToOne()
    {
        var agent = CreateInstance(maxSteps: 0);
        agent.Should().NotBeNull();
    }

    [Fact]
    public void Create_NegativeMaxSteps_ClampsToOne()
    {
        var agent = CreateInstance(maxSteps: -5);
        agent.Should().NotBeNull();
    }

    [Fact]
    public void Create_ReturnsSameInstanceType()
    {
        var agent = CreateInstance();
        agent.Should().BeOfType<Ouroboros.Agent.AgentInstance>();
    }
}
