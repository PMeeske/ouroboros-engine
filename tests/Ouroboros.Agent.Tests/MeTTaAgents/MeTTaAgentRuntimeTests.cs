// <copyright file="MeTTaAgentRuntimeTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Abstractions;
using Ouroboros.Agent.MeTTaAgents;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests.MeTTaAgents;

[Trait("Category", "Unit")]
public class MeTTaAgentRuntimeTests
{
    private readonly Mock<IMeTTaEngine> _engineMock = new();
    private readonly Mock<IAgentProviderFactory> _providerMock = new();

    private MeTTaAgentRuntime CreateRuntime()
    {
        return new MeTTaAgentRuntime(_engineMock.Object, new[] { _providerMock.Object });
    }

    [Fact]
    public void Constructor_NullEngine_Throws()
    {
        var act = () => new MeTTaAgentRuntime(null!, new List<IAgentProviderFactory>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullProviders_Throws()
    {
        var act = () => new MeTTaAgentRuntime(_engineMock.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SpawnedAgents_InitiallyEmpty()
    {
        var runtime = CreateRuntime();
        runtime.SpawnedAgents.Should().BeEmpty();
    }

    [Fact]
    public void Engine_ReturnsInjected()
    {
        var runtime = CreateRuntime();
        runtime.Engine.Should().BeSameAs(_engineMock.Object);
    }

    [Fact]
    public async Task SpawnAgentAsync_NoMatchingProvider_ReturnsFailure()
    {
        var runtime = CreateRuntime();
        _providerMock.Setup(p => p.CanHandle("Ollama")).Returns(false);

        var def = new MeTTaAgentDef("a1", "Ollama", "m", "Coder", "prompt", 4096, 0.5f);

        var result = await runtime.SpawnAgentAsync(def);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No provider factory");
    }

    [Fact]
    public async Task SpawnAgentAsync_ProviderFails_ReturnsFailure()
    {
        var runtime = CreateRuntime();
        var def = new MeTTaAgentDef("a1", "Ollama", "m", "Coder", "prompt", 4096, 0.5f);

        _providerMock.Setup(p => p.CanHandle("Ollama")).Returns(true);
        _providerMock
            .Setup(p => p.CreateModelAsync(def, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IChatCompletionModel, string>.Failure("connection error"));

        var result = await runtime.SpawnAgentAsync(def);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("connection error");
    }

    [Fact]
    public async Task SpawnAgentAsync_Success_AddsAgent()
    {
        var runtime = CreateRuntime();
        var def = new MeTTaAgentDef("a1", "Ollama", "m", "Coder", "prompt", 4096, 0.5f);
        var modelMock = new Mock<IChatCompletionModel>();

        _providerMock.Setup(p => p.CanHandle("Ollama")).Returns(true);
        _providerMock
            .Setup(p => p.CreateModelAsync(def, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IChatCompletionModel, string>.Success(modelMock.Object));
        _engineMock
            .Setup(e => e.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Success(Unit.Value)));

        var result = await runtime.SpawnAgentAsync(def);

        result.IsSuccess.Should().BeTrue();
        runtime.SpawnedAgents.Should().ContainKey("a1");
    }

    [Fact]
    public async Task SpawnAgentAsync_DuplicateId_ReturnsFailure()
    {
        var runtime = CreateRuntime();
        var def = new MeTTaAgentDef("a1", "Ollama", "m", "Coder", "prompt", 4096, 0.5f);
        var modelMock = new Mock<IChatCompletionModel>();

        _providerMock.Setup(p => p.CanHandle("Ollama")).Returns(true);
        _providerMock
            .Setup(p => p.CreateModelAsync(def, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IChatCompletionModel, string>.Success(modelMock.Object));
        _engineMock
            .Setup(e => e.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Success(Unit.Value)));

        await runtime.SpawnAgentAsync(def);
        var result = await runtime.SpawnAgentAsync(def);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already spawned");
    }

    [Fact]
    public async Task ExecuteTaskAsync_AgentNotSpawned_ReturnsFailure()
    {
        var runtime = CreateRuntime();

        var result = await runtime.ExecuteTaskAsync("missing", "t1", "prompt");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not spawned");
    }

    [Fact]
    public async Task TerminateAgentAsync_NotFound_ReturnsFailure()
    {
        var runtime = CreateRuntime();

        var result = await runtime.TerminateAgentAsync("missing");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void ListAgents_Empty_ReturnsMessage()
    {
        var runtime = CreateRuntime();
        runtime.ListAgents().Should().Be("No agents spawned.");
    }

    [Fact]
    public void GetAllStatuses_Empty_ReturnsEmpty()
    {
        var runtime = CreateRuntime();
        runtime.GetAllStatuses().Should().BeEmpty();
    }

    [Fact]
    public void GetAgentsByRole_Empty_ReturnsEmpty()
    {
        var runtime = CreateRuntime();
        runtime.GetAgentsByRole("Coder").Should().BeEmpty();
    }

    [Fact]
    public async Task ExecutePipelineAsync_NullAgentIds_ReturnsFailure()
    {
        var runtime = CreateRuntime();

        var result = await runtime.ExecutePipelineAsync("t", null!, "prompt");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("at least one agent");
    }

    [Fact]
    public async Task ExecutePipelineAsync_EmptyAgentIds_ReturnsFailure()
    {
        var runtime = CreateRuntime();

        var result = await runtime.ExecutePipelineAsync("t", new List<string>(), "prompt");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RouteTaskAsync_InvalidCapability_ReturnsFailure()
    {
        var runtime = CreateRuntime();

        var result = await runtime.RouteTaskAsync("t1", "invalid symbol!", "prompt");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid capability format");
    }
}
