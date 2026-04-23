using System.Diagnostics;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class OrchestrationScopeTests : IDisposable
{
    #region Factory Methods

    [Fact]
    public void ModelSelection_ShouldCreateScope()
    {
        using var scope = OrchestrationScope.ModelSelection("test prompt", "context");

        scope.Should().NotBeNull();
        scope.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
        scope.Activity.Should().NotBeNull();
    }

    [Fact]
    public void ModelSelection_WithoutContext_ShouldCreateScope()
    {
        using var scope = OrchestrationScope.ModelSelection("test prompt");

        scope.Should().NotBeNull();
    }

    [Fact]
    public void Routing_ShouldCreateScope()
    {
        using var scope = OrchestrationScope.Routing("test task");

        scope.Should().NotBeNull();
        scope.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void PlanCreation_ShouldCreateScope()
    {
        using var scope = OrchestrationScope.PlanCreation("goal", 3);

        scope.Should().NotBeNull();
    }

    [Fact]
    public void PlanExecution_ShouldCreateScope()
    {
        using var scope = OrchestrationScope.PlanExecution(Guid.NewGuid(), 5);

        scope.Should().NotBeNull();
    }

    #endregion

    #region Elapsed

    [Fact]
    public void Elapsed_ShouldIncreaseOverTime()
    {
        using var scope = OrchestrationScope.Routing("task");
        var elapsed1 = scope.Elapsed;
        Thread.Sleep(20);
        var elapsed2 = scope.Elapsed;

        elapsed2.Should().BeGreaterOrEqualTo(elapsed1);
    }

    #endregion

    #region CompleteModelSelection

    [Fact]
    public void CompleteModelSelection_ShouldNotThrow()
    {
        using var scope = OrchestrationScope.ModelSelection("prompt");
        scope.CompleteModelSelection("model-1", UseCaseType.General, 0.9);
    }

    [Fact]
    public void CompleteModelSelection_ShouldStopStopwatch()
    {
        using var scope = OrchestrationScope.ModelSelection("prompt");
        scope.CompleteModelSelection("model-1", UseCaseType.General, 0.9);
        var elapsed1 = scope.Elapsed;
        Thread.Sleep(20);
        var elapsed2 = scope.Elapsed;

        elapsed2.Should().Be(elapsed1);
    }

    #endregion

    #region Fail

    [Fact]
    public void Fail_ShouldNotThrow()
    {
        using var scope = OrchestrationScope.Routing("task");
        scope.Fail("error message");
    }

    [Fact]
    public void Fail_ShouldStopStopwatch()
    {
        using var scope = OrchestrationScope.Routing("task");
        scope.Fail("error");
        var elapsed1 = scope.Elapsed;
        Thread.Sleep(20);
        var elapsed2 = scope.Elapsed;

        elapsed2.Should().Be(elapsed1);
    }

    #endregion

    #region RecordException

    [Fact]
    public void RecordException_ShouldNotThrow()
    {
        using var scope = OrchestrationScope.PlanCreation("goal", 2);
        scope.RecordException(new InvalidOperationException("test"));
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_WithoutCompletion_ShouldNotThrow()
    {
        var scope = OrchestrationScope.Routing("task");
        scope.Dispose();
    }

    [Fact]
    public void Dispose_AfterCompletion_ShouldNotThrow()
    {
        var scope = OrchestrationScope.ModelSelection("prompt");
        scope.CompleteModelSelection("model", UseCaseType.General, 0.9);
        scope.Dispose();
    }

    [Fact]
    public void Dispose_AfterFail_ShouldNotThrow()
    {
        var scope = OrchestrationScope.Routing("task");
        scope.Fail("error");
        scope.Dispose();
    }

    [Fact]
    public void Dispose_MultipleTimes_ShouldNotThrow()
    {
        var scope = OrchestrationScope.Routing("task");
        scope.Dispose();
        scope.Dispose();
    }

    #endregion
}
