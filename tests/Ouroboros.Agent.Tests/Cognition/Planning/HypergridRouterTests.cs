using FluentAssertions;
using Ouroboros.Agent.Cognition.Planning;
using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests.Cognition.Planning;

[Trait("Category", "Unit")]
public class HypergridRouterTests
{
    private readonly HypergridRouter _router = new();

    [Fact]
    public void Route_WithSingleStep_ReturnsOneAnnotatedStep()
    {
        var raw = new RawGoalStep("Analyze code", GoalType.Analysis, 0.5);
        var context = HypergridContext.Default;

        var (steps, analysis) = _router.Route(new[] { raw }, context);

        steps.Should().HaveCount(1);
        steps[0].Description.Should().Be("Analyze code");
        steps[0].Type.Should().Be(GoalType.Analysis);
    }

    [Fact]
    public void Route_WithMultipleSteps_AssignsCoordinates()
    {
        var steps = new[]
        {
            new RawGoalStep("Step 1", GoalType.Primary, 0.3),
            new RawGoalStep("Step 2", GoalType.Secondary, 0.6),
            new RawGoalStep("Step 3", GoalType.Primary, 0.9)
        };
        var context = HypergridContext.Default;

        var (routed, analysis) = _router.Route(steps, context);

        routed.Should().HaveCount(3);
        analysis.CausalDepth.Should().Be(0); // no dependencies
    }

    [Fact]
    public void Route_WithDependencies_CalculatesCausalDepth()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();
        var steps = new[]
        {
            new RawGoalStep(id1, "Step 1", GoalType.Primary, 0.5, Array.Empty<Guid>()),
            new RawGoalStep(id2, "Step 2", GoalType.Secondary, 0.5, new[] { id1 }),
            new RawGoalStep(id3, "Step 3", GoalType.Primary, 0.5, new[] { id2 })
        };
        var context = HypergridContext.Default;

        var (_, analysis) = _router.Route(steps, context);

        analysis.CausalDepth.Should().Be(2);
    }

    [Fact]
    public void Route_WithSafetyType_AssignsRequiresApproval()
    {
        var raw = new RawGoalStep("Delete data", GoalType.Safety, 0.9);
        var context = HypergridContext.Default;

        var (steps, _) = _router.Route(new[] { raw }, context);

        steps[0].Mode.Should().Be(ExecutionMode.RequiresApproval);
    }

    [Fact]
    public void Route_WithAvailableTools_AssignsToolDelegation()
    {
        var raw = new RawGoalStep("Search the database", GoalType.Primary, 0.3);
        var context = new HypergridContext(
            Deadline: null,
            AvailableSkills: Array.Empty<string>(),
            AvailableTools: new[] { "search" });

        var (steps, _) = _router.Route(new[] { raw }, context);

        steps[0].Mode.Should().Be(ExecutionMode.ToolDelegation);
    }

    [Fact]
    public void Route_EmptySteps_ReturnsEmptyResult()
    {
        var (steps, analysis) = _router.Route(Array.Empty<RawGoalStep>(), HypergridContext.Default);

        steps.Should().BeEmpty();
        analysis.OverallComplexity.Should().Be(0);
    }

    [Fact]
    public void Route_WithDeadline_AffectsTemporalAxis()
    {
        var raw = new RawGoalStep("Urgent task", GoalType.Primary, 0.8);
        var nearDeadline = new HypergridContext(
            Deadline: DateTimeOffset.UtcNow.AddHours(1),
            AvailableSkills: Array.Empty<string>(),
            AvailableTools: Array.Empty<string>());
        var farDeadline = new HypergridContext(
            Deadline: DateTimeOffset.UtcNow.AddDays(30),
            AvailableSkills: Array.Empty<string>(),
            AvailableTools: Array.Empty<string>());

        var (nearSteps, _) = _router.Route(new[] { raw }, nearDeadline);
        var (farSteps, _) = _router.Route(new[] { raw }, farDeadline);

        nearSteps[0].Coordinate.Temporal.Should().BeGreaterThanOrEqualTo(farSteps[0].Coordinate.Temporal);
    }

    [Fact]
    public void Route_WithSkills_AffectsSemanticAxis()
    {
        var raw = new RawGoalStep("Write Python code", GoalType.Primary, 0.5);
        var withSkills = new HypergridContext(
            Deadline: null,
            AvailableSkills: new[] { "python", "code" },
            AvailableTools: Array.Empty<string>());
        var noSkills = HypergridContext.Default;

        var (withSteps, _) = _router.Route(new[] { raw }, withSkills);
        var (noSteps, _) = _router.Route(new[] { raw }, noSkills);

        withSteps[0].Coordinate.Semantic.Should().BeGreaterThan(noSteps[0].Coordinate.Semantic);
    }

    [Fact]
    public void Route_WithCyclicDependencies_HandlesGracefully()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var steps = new[]
        {
            new RawGoalStep(id1, "Step 1", GoalType.Primary, 0.5, new[] { id2 }),
            new RawGoalStep(id2, "Step 2", GoalType.Primary, 0.5, new[] { id1 })
        };

        var (routed, _) = _router.Route(steps, HypergridContext.Default);

        routed.Should().HaveCount(2);
    }

    [Fact]
    public void Route_HighPriority_IncreasesModalValue()
    {
        var lowPriority = new RawGoalStep("Low priority", GoalType.Primary, 0.1);
        var highPriority = new RawGoalStep("High priority", GoalType.Primary, 0.95);

        var (lowSteps, _) = _router.Route(new[] { lowPriority }, HypergridContext.Default);
        var (highSteps, _) = _router.Route(new[] { highPriority }, HypergridContext.Default);

        highSteps[0].Coordinate.Modal.Should().BeGreaterThan(lowSteps[0].Coordinate.Modal);
    }
}

[Trait("Category", "Unit")]
public class RawGoalStepTests
{
    [Fact]
    public void ShortConstructor_GeneratesNewGuid()
    {
        var step = new RawGoalStep("Do something", GoalType.Primary, 0.5);

        step.Id.Should().NotBeEmpty();
        step.Description.Should().Be("Do something");
        step.Type.Should().Be(GoalType.Primary);
        step.Priority.Should().Be(0.5);
        step.DependsOn.Should().BeEmpty();
    }

    [Fact]
    public void FullConstructor_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var deps = new[] { Guid.NewGuid() };

        var step = new RawGoalStep(id, "Desc", GoalType.Safety, 0.9, deps);

        step.Id.Should().Be(id);
        step.DependsOn.Should().HaveCount(1);
    }

    [Fact]
    public void TwoInstances_WithShortConstructor_HaveDifferentIds()
    {
        var a = new RawGoalStep("Same", GoalType.Primary, 0.5);
        var b = new RawGoalStep("Same", GoalType.Primary, 0.5);

        a.Id.Should().NotBe(b.Id);
    }
}

[Trait("Category", "Unit")]
public class GoalSplitterConfigTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var config = new GoalSplitterConfig();

        config.MaxSteps.Should().Be(8);
        config.MaxRetries.Should().Be(2);
    }

    [Fact]
    public void CustomValues_AreSet()
    {
        var config = new GoalSplitterConfig(MaxSteps: 20, MaxRetries: 5);

        config.MaxSteps.Should().Be(20);
        config.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void RecordEquality_WithSameValues()
    {
        var a = new GoalSplitterConfig(8, 2);
        var b = new GoalSplitterConfig(8, 2);

        a.Should().Be(b);
    }
}
