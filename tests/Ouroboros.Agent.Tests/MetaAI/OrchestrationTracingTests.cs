using System.Diagnostics;
using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class OrchestrationTracingTests
{
    [Fact]
    public void ActivitySource_HasCorrectName()
    {
        OrchestrationTracing.ActivitySource.Name.Should().Be("Ouroboros.Orchestration");
        OrchestrationTracing.ActivitySource.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void Meter_HasCorrectName()
    {
        OrchestrationTracing.Meter.Name.Should().Be("Ouroboros.Orchestration");
        OrchestrationTracing.Meter.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void StartModelSelection_WithNullPrompt_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.StartModelSelection(null);

        act.Should().NotThrow();
    }

    [Fact]
    public void StartModelSelection_WithPrompt_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.StartModelSelection("test prompt", "context");

        act.Should().NotThrow();
    }

    [Fact]
    public void CompleteModelSelection_WithNullActivity_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.CompleteModelSelection(
            null, "gpt-4", UseCaseType.QuestionAnswering, 0.95, TimeSpan.FromMilliseconds(100));

        act.Should().NotThrow();
    }

    [Fact]
    public void StartRouting_WithNullTask_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.StartRouting(null);

        act.Should().NotThrow();
    }

    [Fact]
    public void CompleteRouting_WithNullActivity_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.CompleteRouting(
            null, "local", 0.8, false, TimeSpan.FromMilliseconds(50));

        act.Should().NotThrow();
    }

    [Fact]
    public void CompleteRouting_WithFallback_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.CompleteRouting(
            null, "cloud", 0.5, true, TimeSpan.FromMilliseconds(200));

        act.Should().NotThrow();
    }

    [Fact]
    public void StartPlanCreation_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.StartPlanCreation("test goal", 3);

        act.Should().NotThrow();
    }

    [Fact]
    public void CompletePlanCreation_WithNullActivity_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.CompletePlanCreation(null, 5, 2, TimeSpan.FromSeconds(1));

        act.Should().NotThrow();
    }

    [Fact]
    public void StartPlanExecution_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.StartPlanExecution(Guid.NewGuid(), 10);

        act.Should().NotThrow();
    }

    [Fact]
    public void CompletePlanExecution_WithNullActivity_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.CompletePlanExecution(
            null, 8, 2, TimeSpan.FromSeconds(5));

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordError_WithNullActivity_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.RecordError(null, "test", new Exception("fail"));

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordEvent_WithNoCurrentActivity_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.RecordEvent("test_event",
            new Dictionary<string, object?> { ["key"] = "value" });

        act.Should().NotThrow();
    }
}

[Trait("Category", "Unit")]
public class OrchestrationScopeTests
{
    [Fact]
    public void ModelSelection_CreatesDisposableScope()
    {
        using var scope = OrchestrationScope.ModelSelection("test prompt");

        scope.Should().NotBeNull();
        scope.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void Routing_CreatesDisposableScope()
    {
        using var scope = OrchestrationScope.Routing("test task");

        scope.Should().NotBeNull();
    }

    [Fact]
    public void PlanCreation_CreatesDisposableScope()
    {
        using var scope = OrchestrationScope.PlanCreation("goal", 3);

        scope.Should().NotBeNull();
    }

    [Fact]
    public void PlanExecution_CreatesDisposableScope()
    {
        using var scope = OrchestrationScope.PlanExecution(Guid.NewGuid(), 5);

        scope.Should().NotBeNull();
    }

    [Fact]
    public void Fail_SetsErrorStatus()
    {
        using var scope = OrchestrationScope.ModelSelection("test");

        var act = () => scope.Fail("test error");

        act.Should().NotThrow();
    }

    [Fact]
    public void RecordException_DoesNotThrow()
    {
        using var scope = OrchestrationScope.ModelSelection("test");

        var act = () => scope.RecordException(new InvalidOperationException("test"));

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        var scope = OrchestrationScope.ModelSelection("test");
        scope.Dispose();

        var act = () => scope.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void CompleteModelSelection_ThenDispose_DoesNotThrow()
    {
        var scope = OrchestrationScope.ModelSelection("test");
        scope.CompleteModelSelection("gpt-4", UseCaseType.QuestionAnswering, 0.9);

        var act = () => scope.Dispose();

        act.Should().NotThrow();
    }
}
