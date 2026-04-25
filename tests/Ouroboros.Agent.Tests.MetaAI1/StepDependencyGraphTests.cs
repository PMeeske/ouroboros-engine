using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class StepDependencyGraphTests
{
    #region Constructor

    [Fact]
    public void Constructor_WithSteps_ShouldInitialize()
    {
        var steps = new List<PlanStep>();
        var graph = new StepDependencyGraph(steps);
        graph.Should().NotBeNull();
    }

    #endregion

    #region GetParallelGroups

    [Fact]
    public void GetParallelGroups_EmptySteps_ShouldReturnEmpty()
    {
        var graph = new StepDependencyGraph(new List<PlanStep>());
        var groups = graph.GetParallelGroups();
        groups.Should().BeEmpty();
    }

    [Fact]
    public void GetParallelGroups_SingleStep_ShouldReturnOneGroup()
    {
        var steps = new List<PlanStep>
        {
            new PlanStep("step1", "out1", new Dictionary<string, object>())
        };
        var graph = new StepDependencyGraph(steps);
        var groups = graph.GetParallelGroups();

        groups.Should().ContainSingle();
        groups[0].Should().ContainSingle().Which.Should().Be(0);
    }

    [Fact]
    public void GetParallelGroups_IndependentSteps_ShouldReturnOneGroup()
    {
        var steps = new List<PlanStep>
        {
            new PlanStep("step1", "out1", new Dictionary<string, object> { ["p1"] = "v1" }),
            new PlanStep("step2", "out2", new Dictionary<string, object> { ["p2"] = "v2" }),
            new PlanStep("step3", "out3", new Dictionary<string, object> { ["p3"] = "v3" })
        };
        var graph = new StepDependencyGraph(steps);
        var groups = graph.GetParallelGroups();

        groups.Should().ContainSingle();
        groups[0].Should().HaveCount(3);
    }

    [Fact]
    public void GetParallelGroups_DependentSteps_ShouldReturnMultipleGroups()
    {
        var steps = new List<PlanStep>
        {
            new PlanStep("step1", "out1", new Dictionary<string, object> { ["p1"] = "v1" }),
            new PlanStep("step2", "out2", new Dictionary<string, object> { ["p2"] = "$step1" }),
            new PlanStep("step3", "out3", new Dictionary<string, object> { ["p3"] = "output_step1" })
        };
        var graph = new StepDependencyGraph(steps);
        var groups = graph.GetParallelGroups();

        groups.Should().HaveCountGreaterThanOrEqualTo(2);
        groups[0].Should().Contain(0); // step1 first
    }

    #endregion
}
