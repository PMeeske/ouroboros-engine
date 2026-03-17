using System.Diagnostics;
using FluentAssertions;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class OrchestrationTracingTests
{
    // === Static Members Tests ===

    [Fact]
    public void ActivitySource_IsNotNull()
    {
        OrchestrationTracing.ActivitySource.Should().NotBeNull();
        OrchestrationTracing.ActivitySource.Name.Should().Be("Ouroboros.Orchestration");
    }

    [Fact]
    public void Meter_IsNotNull()
    {
        OrchestrationTracing.Meter.Should().NotBeNull();
        OrchestrationTracing.Meter.Name.Should().Be("Ouroboros.Orchestration");
    }

    // === StartModelSelection Tests ===

    [Fact]
    public void StartModelSelection_WithPrompt_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.StartModelSelection("test prompt", "context");
        act.Should().NotThrow();
    }

    [Fact]
    public void StartModelSelection_NullPrompt_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.StartModelSelection(null);
        act.Should().NotThrow();
    }

    // === CompleteModelSelection Tests ===

    [Fact]
    public void CompleteModelSelection_NullActivity_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.CompleteModelSelection(
            null, "model", UseCaseType.CodeGeneration, 0.9, TimeSpan.FromMilliseconds(100));
        act.Should().NotThrow();
    }

    [Fact]
    public void CompleteModelSelection_WithActivity_SetsTagsOnActivity()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        var activity = OrchestrationTracing.StartModelSelection("prompt");

        var act = () => OrchestrationTracing.CompleteModelSelection(
            activity, "test-model", UseCaseType.Reasoning, 0.85, TimeSpan.FromMilliseconds(200), success: true);
        act.Should().NotThrow();

        activity?.Dispose();
    }

    // === StartRouting Tests ===

    [Fact]
    public void StartRouting_WithTask_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.StartRouting("task");
        act.Should().NotThrow();
    }

    [Fact]
    public void StartRouting_NullTask_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.StartRouting(null);
        act.Should().NotThrow();
    }

    // === CompleteRouting Tests ===

    [Fact]
    public void CompleteRouting_NullActivity_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.CompleteRouting(
            null, "direct", 0.8, false, TimeSpan.FromMilliseconds(50));
        act.Should().NotThrow();
    }

    [Fact]
    public void CompleteRouting_WithFallback_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.CompleteRouting(
            null, "fallback", 0.6, true, TimeSpan.FromMilliseconds(100));
        act.Should().NotThrow();
    }

    // === StartPlanCreation Tests ===

    [Fact]
    public void StartPlanCreation_ValidArgs_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.StartPlanCreation("test goal", 3);
        act.Should().NotThrow();
    }

    // === CompletePlanCreation Tests ===

    [Fact]
    public void CompletePlanCreation_NullActivity_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.CompletePlanCreation(null, 5, 2, TimeSpan.FromSeconds(1));
        act.Should().NotThrow();
    }

    // === StartPlanExecution Tests ===

    [Fact]
    public void StartPlanExecution_ValidArgs_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.StartPlanExecution(Guid.NewGuid(), 5);
        act.Should().NotThrow();
    }

    // === CompletePlanExecution Tests ===

    [Fact]
    public void CompletePlanExecution_NullActivity_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.CompletePlanExecution(null, 3, 1, TimeSpan.FromSeconds(2));
        act.Should().NotThrow();
    }

    // === RecordError Tests ===

    [Fact]
    public void RecordError_NullActivity_DoesNotThrow()
    {
        var ex = new InvalidOperationException("test error");
        var act = () => OrchestrationTracing.RecordError(null, "test_op", ex);
        act.Should().NotThrow();
    }

    // === RecordEvent Tests ===

    [Fact]
    public void RecordEvent_NoCurrentActivity_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.RecordEvent("test_event", new Dictionary<string, object?> { ["key"] = "value" });
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordEvent_NullAttributes_DoesNotThrow()
    {
        var act = () => OrchestrationTracing.RecordEvent("test_event");
        act.Should().NotThrow();
    }
}
