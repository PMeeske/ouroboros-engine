// <copyright file="MeTTaAgentToolTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Abstractions;
using Ouroboros.Agent.MeTTaAgents;

namespace Ouroboros.Tests.MeTTaAgents;

[Trait("Category", "Unit")]
public class MeTTaAgentToolTests
{
    private readonly Mock<IMeTTaEngine> _engineMock = new();

    [Fact]
    public void Constructor_NullRuntime_Throws()
    {
        var act = () => new MeTTaAgentTool(null!, _engineMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullEngine_Throws()
    {
        var runtime = CreateRuntime();
        var act = () => new MeTTaAgentTool(runtime, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Name_ReturnsMeTTaAgents()
    {
        var tool = CreateTool();
        tool.Name.Should().Be("metta_agents");
    }

    [Fact]
    public void Description_IsNotEmpty()
    {
        var tool = CreateTool();
        tool.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void JsonSchema_IsNotNull()
    {
        var tool = CreateTool();
        tool.JsonSchema.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InvokeAsync_InvalidJson_ReturnsFailure()
    {
        var tool = CreateTool();
        var result = await tool.InvokeAsync("not valid json");
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_MissingOperation_ReturnsFailure()
    {
        var tool = CreateTool();
        var result = await tool.InvokeAsync("{}");
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("operation");
    }

    [Fact]
    public async Task InvokeAsync_ListOperation_ReturnsSuccess()
    {
        var tool = CreateTool();
        var result = await tool.InvokeAsync("{\"operation\":\"list\"}");
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_StatusOperation_ReturnsSuccess()
    {
        var tool = CreateTool();
        var result = await tool.InvokeAsync("{\"operation\":\"status\"}");
        result.IsSuccess.Should().BeTrue();
    }

    private MeTTaAgentRuntime CreateRuntime()
    {
        var providers = new List<IAgentProviderFactory>();
        return new MeTTaAgentRuntime(_engineMock.Object, providers);
    }

    private MeTTaAgentTool CreateTool()
    {
        return new MeTTaAgentTool(CreateRuntime(), _engineMock.Object);
    }
}
