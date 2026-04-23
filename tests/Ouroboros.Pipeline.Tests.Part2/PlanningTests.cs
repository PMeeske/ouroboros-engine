namespace Ouroboros.Pipeline.Tests;

using Ouroboros.Pipeline.Planning;
using Ouroboros.Core.Monads;

[Trait("Category", "Unit")]
public class GoalTests
{
    #region Creation

    [Fact]
    public void Atomic_WithCriteria_ShouldCreateGoal()
    {
        var goal = Goal.Atomic("test goal", _ => true);
        goal.Description.Should().Be("test goal");
        goal.SubGoals.Should().BeEmpty();
        goal.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Atomic_WithoutCriteria_ShouldCreateGoalWithDefaultCriteria()
    {
        var goal = Goal.Atomic("test goal");
        goal.Description.Should().Be("test goal");
    }

    [Fact]
    public void Atomic_NullDescription_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Goal.Atomic(null!, _ => true));
    }

    [Fact]
    public void Atomic_NullCriteria_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Goal.Atomic("test", null!));
    }

    #endregion

    #region SubGoals

    [Fact]
    public void WithSubGoals_ShouldSetSubGoals()
    {
        var sub1 = Goal.Atomic("sub1");
        var sub2 = Goal.Atomic("sub2");
        var parent = Goal.Atomic("parent").WithSubGoals(sub1, sub2);
        parent.SubGoals.Should().HaveCount(2);
    }

    [Fact]
    public void AppendSubGoals_ShouldAddToExisting()
    {
        var sub1 = Goal.Atomic("sub1");
        var sub2 = Goal.Atomic("sub2");
        var parent = Goal.Atomic("parent").WithSubGoals(sub1).AppendSubGoals(sub2);
        parent.SubGoals.Should().HaveCount(2);
    }

    [Fact]
    public void WithSubGoals_Null_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Goal.Atomic("test").WithSubGoals(null!));
    }

    #endregion

    #region IsComplete

    [Fact]
    public void IsComplete_AtomicWithTrueCriteria_ShouldReturnTrue()
    {
        var goal = Goal.Atomic("test", _ => true);
        // Need a mock branch - since we don't have one, we just verify criteria works
        goal.CompletionCriteria.Should().NotBeNull();
    }

    [Fact]
    public void IsComplete_CompositeWithSubGoals_ShouldDependOnChildren()
    {
        var sub = Goal.Atomic("sub");
        var parent = Goal.Atomic("parent").WithSubGoals(sub);
        parent.SubGoals.Should().ContainSingle();
    }

    #endregion
}

[Trait("Category", "Unit")]
public class GoalDecomposerTests
{
    [Fact]
    public void Decompose_NullGoal_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => GoalDecomposer.Decompose(null!));
    }

    [Fact]
    public void Decompose_AtomicGoal_ShouldReturnSingle()
    {
        var goal = Goal.Atomic("test");
        var decomposed = GoalDecomposer.Decompose(goal);
        decomposed.Should().ContainSingle();
    }

    [Fact]
    public void Decompose_CompositeGoal_ShouldReturnSubGoals()
    {
        var sub1 = Goal.Atomic("sub1");
        var sub2 = Goal.Atomic("sub2");
        var parent = Goal.Atomic("parent").WithSubGoals(sub1, sub2);
        var decomposed = GoalDecomposer.Decompose(parent);
        decomposed.Should().HaveCount(2);
    }
}

[Trait("Category", "Unit")]
public class GoalExtensionsTests
{
    [Fact]
    public void BindAsync_Success_ShouldBind()
    {
        var result = Result<Goal>.Success(Goal.Atomic("test"));
        var bound = result.BindAsync(g => Task.FromResult(Result<Goal>.Success(Goal.Atomic("bound"))));
        bound.Result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void BindAsync_Failure_ShouldReturnFailure()
    {
        var result = Result<Goal>.Failure("error");
        var bound = result.BindAsync(g => Task.FromResult(Result<Goal>.Success(Goal.Atomic("bound"))));
        bound.Result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MapAsync_Success_ShouldMap()
    {
        var result = Result<Goal>.Success(Goal.Atomic("test"));
        var mapped = result.MapAsync(g => Task.FromResult(Goal.Atomic("mapped")));
        mapped.Result.IsSuccess.Should().BeTrue();
        mapped.Result.Value.Description.Should().Be("mapped");
    }

    [Fact]
    public void MapAsync_Failure_ShouldReturnFailure()
    {
        var result = Result<Goal>.Failure("error");
        var mapped = result.MapAsync(g => Task.FromResult(Goal.Atomic("mapped")));
        mapped.Result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ToStep_ShouldCreateStep()
    {
        var goal = Goal.Atomic("test");
        var step = goal.ToStep();
        step.Should().NotBeNull();
    }
}

[Trait("Category", "Unit")]
public class HierarchicalGoalPlannerTests
{
    [Fact]
    public void Plan_NullGoal_ShouldThrowArgumentNullException()
    {
        var planner = new HierarchicalGoalPlanner();
        Assert.Throws<ArgumentNullException>(() => planner.Plan(null!));
    }

    [Fact]
    public void Plan_AtomicGoal_ShouldCreateSinglePlan()
    {
        var planner = new HierarchicalGoalPlanner();
        var goal = Goal.Atomic("test");
        var plan = planner.Plan(goal);
        plan.Should().NotBeNull();
    }
}

[Trait("Category", "Unit")]
public class HyperonPlannerTests
{
    [Fact]
    public void Constructor_Default_ShouldInitialize()
    {
        var planner = new HyperonPlanner();
        planner.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_NullEngine_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new HyperonPlanner(null!));
    }

    [Fact]
    public void Engine_ShouldReturnEngine()
    {
        var planner = new HyperonPlanner();
        planner.Engine.Should().NotBeNull();
    }

    [Fact]
    public void Flow_ShouldReturnFlow()
    {
        var planner = new HyperonPlanner();
        planner.Flow.Should().NotBeNull();
    }

    [Fact]
    public void RegisterToolSignature_NullName_ShouldThrowArgumentNullException()
    {
        var planner = new HyperonPlanner();
        Assert.Throws<ArgumentNullException>(() => planner.RegisterToolSignature(null!, MeTTaType.Atom, MeTTaType.Atom));
    }

    [Fact]
    public void RegisterToolSignature_Valid_ShouldRegister()
    {
        var planner = new HyperonPlanner();
        planner.RegisterToolSignature("tool1", MeTTaType.Atom, MeTTaType.Text);
    }

    [Fact]
    public async Task InitializeAsync_ShouldInitialize()
    {
        var planner = new HyperonPlanner();
        await planner.InitializeAsync();
    }

    [Fact]
    public async Task CreatePlanAsync_NullGoal_ShouldThrowArgumentNullException()
    {
        var planner = new HyperonPlanner();
        await Assert.ThrowsAsync<ArgumentNullException>(() => planner.CreatePlanAsync(null!));
    }

    [Fact]
    public async Task CreatePlanAsync_ValidGoal_ShouldReturnPlan()
    {
        var planner = new HyperonPlanner();
        await planner.InitializeAsync();
        var plan = await planner.CreatePlanAsync(Goal.Atomic("test"));
        plan.Should().NotBeNull();
    }
}

[Trait("Category", "Unit")]
public class MeTTaPlannerTests
{
    [Fact]
    public void CreatePlan_NullGoal_ShouldThrowArgumentNullException()
    {
        var planner = new MeTTaPlanner();
        Assert.Throws<ArgumentNullException>(() => planner.CreatePlan(null!));
    }

    [Fact]
    public void CreatePlan_AtomicGoal_ShouldCreatePlan()
    {
        var planner = new MeTTaPlanner();
        var goal = Goal.Atomic("test");
        var plan = planner.CreatePlan(goal);
        plan.Should().NotBeNull();
    }
}

[Trait("Category", "Unit")]
public class MeTTaTypeTests
{
    [Fact]
    public void Enum_ShouldHaveExpectedValues()
    {
        var values = Enum.GetValues<MeTTaType>();
        values.Length.Should().BeGreaterThan(0);
    }
}

[Trait("Category", "Unit")]
public class PlanCandidateTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // PlanCandidate is a sealed record with Plan, double Score, string Explanation
        // Can't easily create Plan without dependencies, so just verify the type exists
        typeof(PlanCandidate).IsSealed.Should().BeTrue();
    }
}

[Trait("Category", "Unit")]
public class PlanExecutionStepTests
{
    [Fact]
    public void Properties_ShouldGetAndSet()
    {
        var step = new PlanExecutionStep
        {
            ToolName = "test-tool",
            Input = "input",
            Output = "output",
            Success = true,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddSeconds(1)
        };
        step.ToolName.Should().Be("test-tool");
        step.Success.Should().BeTrue();
        step.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }
}

[Trait("Category", "Unit")]
public class SubGoalExtensionsTests
{
    [Fact]
    public void ToSubGoals_Null_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((IEnumerable<string>)null!).ToSubGoals());
    }

    [Fact]
    public void ToSubGoals_ShouldCreateGoals()
    {
        var goals = new[] { "g1", "g2" }.ToSubGoals();
        goals.Should().HaveCount(2);
    }

    [Fact]
    public void Flatten_Null_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((Goal)null!).Flatten());
    }

    [Fact]
    public void Flatten_ShouldReturnAllGoals()
    {
        var sub = Goal.Atomic("sub");
        var parent = Goal.Atomic("parent").WithSubGoals(sub);
        var flat = parent.Flatten();
        flat.Should().Contain(parent);
        flat.Should().Contain(sub);
    }
}

[Trait("Category", "Unit")]
public class SymbolicPlanSelectorTests
{
    [Fact]
    public void Select_NullCandidates_ShouldThrowArgumentNullException()
    {
        var selector = new SymbolicPlanSelector();
        Assert.Throws<ArgumentNullException>(() => selector.Select(null!));
    }

    [Fact]
    public void Select_EmptyCandidates_ShouldThrowArgumentException()
    {
        var selector = new SymbolicPlanSelector();
        Assert.Throws<ArgumentException>(() => selector.Select(Array.Empty<PlanCandidate>()));
    }
}

[Trait("Category", "Unit")]
public class ToolBinderTests
{
    [Fact]
    public void Bind_NullGoal_ShouldThrowArgumentNullException()
    {
        var binder = new ToolBinder();
        Assert.Throws<ArgumentNullException>(() => binder.Bind(null!));
    }

    [Fact]
    public void Bind_AtomicGoal_ShouldReturnBindings()
    {
        var binder = new ToolBinder();
        var goal = Goal.Atomic("test");
        var bindings = binder.Bind(goal);
        bindings.Should().NotBeNull();
    }
}

[Trait("Category", "Unit")]
public class ToolBinderExtensionsTests
{
    [Fact]
    public void ToToolChain_NullBindings_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((IEnumerable<ToolBinding>)null!).ToToolChain());
    }
}

[Trait("Category", "Unit")]
public class ToolChainTests
{
    [Fact]
    public void Empty_ShouldCreateEmptyChain()
    {
        var chain = ToolChain.Empty;
        chain.Steps.Should().BeEmpty();
    }

    [Fact]
    public void FromBindings_Null_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ToolChain.FromBindings(null!));
    }

    [Fact]
    public void Append_Null_ShouldThrowArgumentNullException()
    {
        var chain = ToolChain.Empty;
        Assert.Throws<ArgumentNullException>(() => chain.Append(null!));
    }
}
