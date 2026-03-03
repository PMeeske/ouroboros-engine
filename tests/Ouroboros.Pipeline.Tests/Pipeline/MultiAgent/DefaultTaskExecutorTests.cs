// <copyright file="DefaultTaskExecutorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using FluentAssertions;
using Xunit;
using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Tests.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public sealed class DefaultTaskExecutorTests
{
    private readonly DefaultTaskExecutor _executor = new();

    private static AgentState CreateAgentState(string name = "TestAgent")
    {
        var identity = new AgentIdentity(
            Guid.NewGuid(), name, AgentRole.Worker,
            ImmutableList<AgentCapability>.Empty,
            ImmutableDictionary<string, object>.Empty,
            DateTime.UtcNow);
        return AgentState.ForAgent(identity);
    }

    private static AgentTask CreateTask(string description = "Test task")
    {
        var goal = Goal.Atomic(description);
        return AgentTask.Create(goal);
    }

    [Fact]
    public async Task ExecuteAsync_NullTask_Throws()
    {
        Func<Task> act = () => _executor.ExecuteAsync(null!, CreateAgentState(), CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_NullAgent_Throws()
    {
        Func<Task> act = () => _executor.ExecuteAsync(CreateTask(), null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_ValidInputs_ReturnsSuccess()
    {
        var task = CreateTask("Do something");
        var agent = CreateAgentState("Alice");

        var result = await _executor.ExecuteAsync(task, agent, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Output.Should().Contain("Alice");
        result.Output.Should().Contain("Do something");
    }

    [Fact]
    public async Task ExecuteAsync_CancellationBeforeExecution_ReturnsTimeout()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var task = CreateTask();
        var agent = CreateAgentState();

        Func<Task> act = () => _executor.ExecuteAsync(task, agent, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_OutputContainsRole()
    {
        var task = CreateTask("Analyze data");
        var agent = CreateAgentState("Bob");

        var result = await _executor.ExecuteAsync(task, agent, CancellationToken.None);

        result.Output.Should().Contain("Worker");
    }
}
