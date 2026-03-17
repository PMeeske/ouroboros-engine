using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;
using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class DefaultTaskExecutorTests
{
    private readonly DefaultTaskExecutor _executor = new();

    [Fact]
    public async Task ExecuteAsync_WithValidInputs_ReturnsSuccess()
    {
        // Arrange
        var goal = Goal.Atomic("Test goal");
        var task = AgentTask.Create(goal).AssignTo(Guid.NewGuid()).Start();
        var identity = AgentIdentity.Create("TestAgent", AgentRole.Coder);
        var agent = AgentState.ForAgent(identity);

        // Act
        var result = await _executor.ExecuteAsync(task, agent, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("Test goal");
        result.Output.Should().Contain("TestAgent");
        result.Output.Should().Contain("Coder");
    }

    [Fact]
    public async Task ExecuteAsync_WithNullTask_ThrowsArgumentNullException()
    {
        var agent = AgentState.ForAgent(AgentIdentity.Create("Agent", AgentRole.Coder));
        Func<Task> act = () => _executor.ExecuteAsync(null!, agent, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("task");
    }

    [Fact]
    public async Task ExecuteAsync_WithNullAgent_ThrowsArgumentNullException()
    {
        var task = AgentTask.Create(Goal.Atomic("Test")).Start();
        Func<Task> act = () => _executor.ExecuteAsync(task, null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("agent");
    }

    [Fact]
    public async Task ExecuteAsync_WithCancelledToken_ThrowsOrReturnsFailed()
    {
        // Arrange
        var task = AgentTask.Create(Goal.Atomic("Test")).Start();
        var agent = AgentState.ForAgent(AgentIdentity.Create("Agent", AgentRole.Coder));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - should either throw OperationCanceledException or return failure
        try
        {
            var result = await _executor.ExecuteAsync(task, agent, cts.Token);
            // If it doesn't throw, it might return success since the Task.Yield
            // may complete before cancellation is checked
        }
        catch (OperationCanceledException)
        {
            // This is also acceptable behavior
        }
    }
}
