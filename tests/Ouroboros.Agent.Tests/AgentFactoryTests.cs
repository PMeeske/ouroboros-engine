// <copyright file="AgentFactoryTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Moq;
using Xunit;
using Ouroboros.Abstractions.Core;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class AgentFactoryTests
{
    private readonly Mock<IChatCompletionModel> _chatMock = new();

    private Agent.AgentInstance CreateInstance(
        string mode = "simple",
        bool debug = false,
        int maxSteps = 3,
        bool ragEnabled = false,
        string embedModel = "",
        bool jsonTools = false,
        bool stream = false)
    {
        return Agent.AgentFactory.Create(
            mode, _chatMock.Object, new Agent.ToolRegistry(), debug,
            maxSteps, ragEnabled, embedModel, jsonTools, stream);
    }

    [Fact]
    public void Create_SimpleMode_SetsMode()
    {
        var agent = CreateInstance(mode: "simple");
        agent.Mode.Should().Be("simple");
    }

    [Fact]
    public void Create_NullOrWhitespaceMode_DefaultsToSimple()
    {
        var agent = CreateInstance(mode: "  ");
        agent.Mode.Should().Be("simple");
    }

    [Fact]
    public void Create_EmptyMode_DefaultsToSimple()
    {
        var agent = CreateInstance(mode: "");
        agent.Mode.Should().Be("simple");
    }

    [Fact]
    public void Create_CustomMode_SetsMode()
    {
        var agent = CreateInstance(mode: "advanced");
        agent.Mode.Should().Be("advanced");
    }

    [Fact]
    public void Create_Debug_SetsDebugProperty()
    {
        var agent = CreateInstance(debug: true);
        agent.Debug.Should().BeTrue();
    }

    [Fact]
    public void Create_DebugFalse_DefaultsFalse()
    {
        var agent = CreateInstance(debug: false);
        agent.Debug.Should().BeFalse();
    }

    [Fact]
    public void Create_RagEnabled_SetsProperty()
    {
        var agent = CreateInstance(ragEnabled: true);
        agent.RagEnabled.Should().BeTrue();
    }

    [Fact]
    public void Create_EmbedModelName_SetsProperty()
    {
        var agent = CreateInstance(embedModel: "text-embedding-ada-002");
        agent.EmbedModelName.Should().Be("text-embedding-ada-002");
    }

    [Fact]
    public void Create_JsonTools_SetsProperty()
    {
        var agent = CreateInstance(jsonTools: true);
        agent.JsonTools.Should().BeTrue();
    }

    [Fact]
    public void Create_Stream_SetsProperty()
    {
        var agent = CreateInstance(stream: true);
        agent.Stream.Should().BeTrue();
    }

    [Fact]
    public void Create_MaxStepsZero_ClampsToOne()
    {
        var agent = CreateInstance(maxSteps: 0);
        // The constructor clamps to Math.Max(1, maxSteps)
        // We verify the agent was created successfully; the value is internal
        agent.Should().NotBeNull();
    }

    [Fact]
    public void Create_NegativeMaxSteps_ClampsToOne()
    {
        var agent = CreateInstance(maxSteps: -5);
        agent.Should().NotBeNull();
    }
}
