using FluentAssertions;
using NSubstitute;
using LangChain.Databases;
using Ouroboros.Pipeline.Branches;
using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Tests.Planning;

[Trait("Category", "Unit")]
public sealed class HierarchicalGoalPlannerTests
{
    private static PipelineBranch CreateBranch()
    {
        var store = Substitute.For<IVectorStore>();
        return new PipelineBranch("test", store, new DataSource("test", "test"));
    }

    #region ExecuteGoalArrow

    [Fact]
    public void ExecuteGoalArrow_NullGoal_ThrowsArgumentNullException()
    {
        // Act
        var act = () => HierarchicalGoalPlanner.ExecuteGoalArrow(
            null!,
            _ => branch => Task.FromResult(branch));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ExecuteGoalArrow_NullStepSelector_ThrowsArgumentNullException()
    {
        // Act
        var act = () => HierarchicalGoalPlanner.ExecuteGoalArrow(
            Goal.Atomic("Test"),
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteGoalArrow_GoalAlreadyComplete_ReturnsBranchUnchanged()
    {
        // Arrange
        var goal = Goal.Atomic("Test", _ => true);
        var branch = CreateBranch();
        var selectorCalled = false;

        var step = HierarchicalGoalPlanner.ExecuteGoalArrow(
            goal,
            _ => { selectorCalled = true; return b => Task.FromResult(b); });

        // Act
        var result = await step(branch);

        // Assert
        result.Should().Be(branch);
        selectorCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteGoalArrow_AtomicGoal_ExecutesStep()
    {
        // Arrange
        var goal = Goal.Atomic("Test", _ => false);
        var branch = CreateBranch();
        var executed = false;

        var step = HierarchicalGoalPlanner.ExecuteGoalArrow(
            goal,
            _ => b => { executed = true; return Task.FromResult(b); });

        // Act
        await step(branch);

        // Assert
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteGoalArrow_CompositeGoal_ExecutesIncompleteSubGoals()
    {
        // Arrange
        var executedGoals = new List<string>();
        var sub1 = Goal.Atomic("Sub1", _ => true); // already complete
        var sub2 = Goal.Atomic("Sub2", _ => false);
        var sub3 = Goal.Atomic("Sub3", _ => false);
        var parent = Goal.Atomic("Parent").WithSubGoals(sub1, sub2, sub3);
        var branch = CreateBranch();

        var step = HierarchicalGoalPlanner.ExecuteGoalArrow(
            parent,
            g =>
            {
                executedGoals.Add(g.Description);
                return b => Task.FromResult(b);
            });

        // Act
        await step(branch);

        // Assert
        executedGoals.Should().Contain("Sub2");
        executedGoals.Should().Contain("Sub3");
        executedGoals.Should().NotContain("Sub1"); // already complete
    }

    #endregion

    #region ExecuteGoalSafeArrow

    [Fact]
    public void ExecuteGoalSafeArrow_NullGoal_ThrowsArgumentNullException()
    {
        // Act
        var act = () => HierarchicalGoalPlanner.ExecuteGoalSafeArrow(
            null!,
            _ => branch => Task.FromResult(Result<PipelineBranch>.Success(branch)));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteGoalSafeArrow_GoalComplete_ReturnsSuccess()
    {
        // Arrange
        var goal = Goal.Atomic("Done", _ => true);
        var branch = CreateBranch();

        var step = HierarchicalGoalPlanner.ExecuteGoalSafeArrow(
            goal,
            _ => b => Task.FromResult(Result<PipelineBranch>.Success(b)));

        // Act
        var result = await step(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteGoalSafeArrow_AtomicGoal_ExecutesStep()
    {
        // Arrange
        var goal = Goal.Atomic("Work");
        var branch = CreateBranch();

        var step = HierarchicalGoalPlanner.ExecuteGoalSafeArrow(
            goal,
            _ => b => Task.FromResult(Result<PipelineBranch>.Success(b)));

        // Act
        var result = await step(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteGoalSafeArrow_StepReturnsFailure_PropagatesError()
    {
        // Arrange
        var sub1 = Goal.Atomic("Fails");
        var sub2 = Goal.Atomic("Should not run");
        var parent = Goal.Atomic("Parent").WithSubGoals(sub1, sub2);
        var branch = CreateBranch();

        var step = HierarchicalGoalPlanner.ExecuteGoalSafeArrow(
            parent,
            _ => _ => Task.FromResult(Result<PipelineBranch>.Failure("Step failed")));

        // Act
        var result = await step(branch);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Step failed");
    }

    [Fact]
    public async Task ExecuteGoalSafeArrow_StepThrowsException_ReturnsFailure()
    {
        // Arrange
        var goal = Goal.Atomic("Throws");
        var branch = CreateBranch();

        var step = HierarchicalGoalPlanner.ExecuteGoalSafeArrow(
            goal,
            _ => _ => throw new InvalidOperationException("Boom"));

        // Act
        var result = await step(branch);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Boom");
    }

    #endregion

    #region ExecuteGoalParallelArrow

    [Fact]
    public void ExecuteGoalParallelArrow_NullGoal_ThrowsArgumentNullException()
    {
        // Act
        var act = () => HierarchicalGoalPlanner.ExecuteGoalParallelArrow(
            null!,
            _ => branch => Task.FromResult(Result<PipelineBranch>.Success(branch)));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteGoalParallelArrow_GoalComplete_ReturnsSuccess()
    {
        // Arrange
        var goal = Goal.Atomic("Complete", _ => true);
        var branch = CreateBranch();

        var step = HierarchicalGoalPlanner.ExecuteGoalParallelArrow(goal,
            _ => b => Task.FromResult(Result<PipelineBranch>.Success(b)));

        // Act
        var result = await step(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteGoalParallelArrow_AtomicGoal_ExecutesStep()
    {
        // Arrange
        var goal = Goal.Atomic("Work");
        var branch = CreateBranch();

        var step = HierarchicalGoalPlanner.ExecuteGoalParallelArrow(goal,
            _ => b => Task.FromResult(Result<PipelineBranch>.Success(b)));

        // Act
        var result = await step(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteGoalParallelArrow_MultipleSubGoals_ExecutesAll()
    {
        // Arrange
        var counter = 0;
        var sub1 = Goal.Atomic("Sub1");
        var sub2 = Goal.Atomic("Sub2");
        var sub3 = Goal.Atomic("Sub3");
        var parent = Goal.Atomic("Parent").WithSubGoals(sub1, sub2, sub3);
        var branch = CreateBranch();

        var step = HierarchicalGoalPlanner.ExecuteGoalParallelArrow(parent,
            _ => b =>
            {
                Interlocked.Increment(ref counter);
                return Task.FromResult(Result<PipelineBranch>.Success(b));
            });

        // Act
        var result = await step(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        counter.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteGoalParallelArrow_AnySubGoalFails_ReturnsFailure()
    {
        // Arrange
        var sub1 = Goal.Atomic("OK");
        var sub2 = Goal.Atomic("Fails");
        var parent = Goal.Atomic("Parent").WithSubGoals(sub1, sub2);
        var branch = CreateBranch();
        var callCount = 0;

        var step = HierarchicalGoalPlanner.ExecuteGoalParallelArrow(parent,
            g => b =>
            {
                Interlocked.Increment(ref callCount);
                if (g.Description == "Fails")
                    return Task.FromResult(Result<PipelineBranch>.Failure("Failed sub-goal"));
                return Task.FromResult(Result<PipelineBranch>.Success(b));
            });

        // Act
        var result = await step(branch);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Parallel goal execution failed");
    }

    #endregion
}
