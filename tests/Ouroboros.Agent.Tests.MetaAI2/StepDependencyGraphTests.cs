using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class StepDependencyGraphTests
{
    private readonly StepDependencyGraph _graph;

    public StepDependencyGraphTests()
    {
        _graph = new StepDependencyGraph();
    }

    #region AddStep

    [Fact]
    public void AddStep_WithNullStep_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => _graph.AddStep(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("step");
    }

    [Fact]
    public void AddStep_NewStep_ShouldAdd()
    {
        // Arrange
        var step = new PlanStep("action", new Dictionary<string, object>(), "outcome", 0.8);

        // Act
        _graph.AddStep(step);

        // Assert
        _graph.StepCount.Should().Be(1);
    }

    [Fact]
    public void AddStep_DuplicateStep_ShouldNotAddTwice()
    {
        // Arrange
        var step = new PlanStep("action", new Dictionary<string, object>(), "outcome", 0.8);

        // Act
        _graph.AddStep(step);
        _graph.AddStep(step);

        // Assert
        _graph.StepCount.Should().Be(1);
    }

    #endregion

    #region AddDependency

    [Fact]
    public void AddDependency_BetweenExistingSteps_ShouldAdd()
    {
        // Arrange
        var step1 = new PlanStep("action1", new Dictionary<string, object>(), "outcome1", 0.8);
        var step2 = new PlanStep("action2", new Dictionary<string, object>(), "outcome2", 0.9);
        _graph.AddStep(step1);
        _graph.AddStep(step2);

        // Act
        _graph.AddDependency(step1, step2);

        // Assert
        var dependents = _graph.GetDependents(step1);
        dependents.Should().ContainSingle().Which.Should().Be(step2);
    }

    [Fact]
    public void AddDependency_SelfReference_ShouldNotAdd()
    {
        // Arrange
        var step = new PlanStep("action", new Dictionary<string, object>(), "outcome", 0.8);
        _graph.AddStep(step);

        // Act
        _graph.AddDependency(step, step);

        // Assert
        _graph.GetDependents(step).Should().BeEmpty();
    }

    [Fact]
    public void AddDependency_StepNotInGraph_ShouldNotAdd()
    {
        // Arrange
        var step1 = new PlanStep("action1", new Dictionary<string, object>(), "outcome1", 0.8);
        var step2 = new PlanStep("action2", new Dictionary<string, object>(), "outcome2", 0.9);
        _graph.AddStep(step1);

        // Act
        _graph.AddDependency(step1, step2);

        // Assert
        _graph.GetDependents(step1).Should().BeEmpty();
    }

    #endregion

    #region GetExecutionOrder

    [Fact]
    public void GetExecutionOrder_EmptyGraph_ShouldReturnEmpty()
    {
        // Act
        var result = _graph.GetExecutionOrder();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetExecutionOrder_SingleStep_ShouldReturnStep()
    {
        // Arrange
        var step = new PlanStep("action", new Dictionary<string, object>(), "outcome", 0.8);
        _graph.AddStep(step);

        // Act
        var result = _graph.GetExecutionOrder();

        // Assert
        result.Should().ContainSingle().Which.Should().Be(step);
    }

    [Fact]
    public void GetExecutionOrder_TwoStepsWithDependency_ShouldOrderCorrectly()
    {
        // Arrange
        var step1 = new PlanStep("action1", new Dictionary<string, object>(), "outcome1", 0.8);
        var step2 = new PlanStep("action2", new Dictionary<string, object>(), "outcome2", 0.9);
        _graph.AddStep(step1);
        _graph.AddStep(step2);
        _graph.AddDependency(step1, step2);

        // Act
        var result = _graph.GetExecutionOrder();

        // Assert
        result.Should().HaveCount(2);
        result[0].Should().Be(step1);
        result[1].Should().Be(step2);
    }

    [Fact]
    public void GetExecutionOrder_MultipleDependencies_ShouldOrderCorrectly()
    {
        // Arrange
        var step1 = new PlanStep("action1", new Dictionary<string, object>(), "outcome1", 0.8);
        var step2 = new PlanStep("action2", new Dictionary<string, object>(), "outcome2", 0.9);
        var step3 = new PlanStep("action3", new Dictionary<string, object>(), "outcome3", 0.7);
        _graph.AddStep(step1);
        _graph.AddStep(step2);
        _graph.AddStep(step3);
        _graph.AddDependency(step1, step2);
        _graph.AddDependency(step2, step3);

        // Act
        var result = _graph.GetExecutionOrder();

        // Assert
        result.Should().HaveCount(3);
        result[0].Should().Be(step1);
        result[1].Should().Be(step2);
        result[2].Should().Be(step3);
    }

    #endregion

    #region GetIndependentSteps

    [Fact]
    public void GetIndependentSteps_EmptyGraph_ShouldReturnEmpty()
    {
        // Act
        var result = _graph.GetIndependentSteps();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetIndependentSteps_SingleStep_ShouldReturnIt()
    {
        // Arrange
        var step = new PlanStep("action", new Dictionary<string, object>(), "outcome", 0.8);
        _graph.AddStep(step);

        // Act
        var result = _graph.GetIndependentSteps();

        // Assert
        result.Should().ContainSingle().Which.Should().Be(step);
    }

    [Fact]
    public void GetIndependentSteps_TwoStepsWithDependency_ShouldReturnIndependentOnly()
    {
        // Arrange
        var step1 = new PlanStep("action1", new Dictionary<string, object>(), "outcome1", 0.8);
        var step2 = new PlanStep("action2", new Dictionary<string, object>(), "outcome2", 0.9);
        _graph.AddStep(step1);
        _graph.AddStep(step2);
        _graph.AddDependency(step1, step2);

        // Act
        var result = _graph.GetIndependentSteps();

        // Assert
        result.Should().ContainSingle().Which.Should().Be(step1);
    }

    #endregion

    #region GetPrerequisites

    [Fact]
    public void GetPrerequisites_StepWithNoDependencies_ShouldReturnEmpty()
    {
        // Arrange
        var step = new PlanStep("action", new Dictionary<string, object>(), "outcome", 0.8);
        _graph.AddStep(step);

        // Act
        var result = _graph.GetPrerequisites(step);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetPrerequisites_StepWithDependencies_ShouldReturnPrerequisites()
    {
        // Arrange
        var step1 = new PlanStep("action1", new Dictionary<string, object>(), "outcome1", 0.8);
        var step2 = new PlanStep("action2", new Dictionary<string, object>(), "outcome2", 0.9);
        _graph.AddStep(step1);
        _graph.AddStep(step2);
        _graph.AddDependency(step1, step2);

        // Act
        var result = _graph.GetPrerequisites(step2);

        // Assert
        result.Should().ContainSingle().Which.Should().Be(step1);
    }

    #endregion

    #region GetDependents

    [Fact]
    public void GetDependents_StepWithNoDependents_ShouldReturnEmpty()
    {
        // Arrange
        var step = new PlanStep("action", new Dictionary<string, object>(), "outcome", 0.8);
        _graph.AddStep(step);

        // Act
        var result = _graph.GetDependents(step);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetDependents_StepWithDependents_ShouldReturnDependents()
    {
        // Arrange
        var step1 = new PlanStep("action1", new Dictionary<string, object>(), "outcome1", 0.8);
        var step2 = new PlanStep("action2", new Dictionary<string, object>(), "outcome2", 0.9);
        _graph.AddStep(step1);
        _graph.AddStep(step2);
        _graph.AddDependency(step1, step2);

        // Act
        var result = _graph.GetDependents(step1);

        // Assert
        result.Should().ContainSingle().Which.Should().Be(step2);
    }

    #endregion

    #region HasCircularDependencies

    [Fact]
    public void HasCircularDependencies_AcyclicGraph_ShouldReturnFalse()
    {
        // Arrange
        var step1 = new PlanStep("action1", new Dictionary<string, object>(), "outcome1", 0.8);
        var step2 = new PlanStep("action2", new Dictionary<string, object>(), "outcome2", 0.9);
        _graph.AddStep(step1);
        _graph.AddStep(step2);
        _graph.AddDependency(step1, step2);

        // Act
        var result = _graph.HasCircularDependencies();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasCircularDependencies_CyclicGraph_ShouldReturnTrue()
    {
        // Arrange
        var step1 = new PlanStep("action1", new Dictionary<string, object>(), "outcome1", 0.8);
        var step2 = new PlanStep("action2", new Dictionary<string, object>(), "outcome2", 0.9);
        _graph.AddStep(step1);
        _graph.AddStep(step2);
        _graph.AddDependency(step1, step2);
        _graph.AddDependency(step2, step1);

        // Act
        var result = _graph.HasCircularDependencies();

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region IsIndependent

    [Fact]
    public void IsIndependent_IndependentStep_ShouldReturnTrue()
    {
        // Arrange
        var step = new PlanStep("action", new Dictionary<string, object>(), "outcome", 0.8);
        _graph.AddStep(step);

        // Act
        var result = _graph.IsIndependent(step);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsIndependent_DependentStep_ShouldReturnFalse()
    {
        // Arrange
        var step1 = new PlanStep("action1", new Dictionary<string, object>(), "outcome1", 0.8);
        var step2 = new PlanStep("action2", new Dictionary<string, object>(), "outcome2", 0.9);
        _graph.AddStep(step1);
        _graph.AddStep(step2);
        _graph.AddDependency(step1, step2);

        // Act
        var result = _graph.IsIndependent(step2);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region BuildFromPlan

    [Fact]
    public void BuildFromPlan_NullPlan_ShouldReturnEmptyGraph()
    {
        // Act
        var graph = StepDependencyGraph.BuildFromPlan(null!);

        // Assert
        graph.StepCount.Should().Be(0);
    }

    [Fact]
    public void BuildFromPlan_EmptyPlan_ShouldReturnEmptyGraph()
    {
        // Arrange
        var plan = new ExecutionPlan("goal", new List<PlanStep>());

        // Act
        var graph = StepDependencyGraph.BuildFromPlan(plan);

        // Assert
        graph.StepCount.Should().Be(0);
    }

    [Fact]
    public void BuildFromPlan_SingleStep_ShouldCreateGraph()
    {
        // Arrange
        var step = new PlanStep("action", new Dictionary<string, object>(), "outcome", 0.8);
        var plan = new ExecutionPlan("goal", new List<PlanStep> { step });

        // Act
        var graph = StepDependencyGraph.BuildFromPlan(plan);

        // Assert
        graph.StepCount.Should().Be(1);
    }

    [Fact]
    public void BuildFromPlan_MultipleSteps_ShouldCreateGraph()
    {
        // Arrange
        var step1 = new PlanStep("action1", new Dictionary<string, object>(), "outcome1", 0.8);
        var step2 = new PlanStep("action2", new Dictionary<string, object>(), "outcome2", 0.9);
        var plan = new ExecutionPlan("goal", new List<PlanStep> { step1, step2 });

        // Act
        var graph = StepDependencyGraph.BuildFromPlan(plan);

        // Assert
        graph.StepCount.Should().Be(2);
    }

    #endregion
}
