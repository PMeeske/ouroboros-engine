using FluentAssertions;
using NSubstitute;
using LangChain.Databases;
using Ouroboros.Pipeline.Branches;
using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Tests.Planning;

[Trait("Category", "Unit")]
public sealed class GoalExtensionsTests
{
    private static PipelineBranch CreateBranch()
    {
        var store = Substitute.For<IVectorStore>();
        return new PipelineBranch("test", store, new DataSource("test", "test"));
    }

    #region BindAsync (Result<Goal>)

    [Fact]
    public async Task BindAsync_SuccessResult_AppliesBinder()
    {
        // Arrange
        var goal = Goal.Atomic("Original");
        var result = Result<Goal>.Success(goal);
        Func<Goal, Task<Result<Goal>>> binder = g =>
            Task.FromResult(Result<Goal>.Success(Goal.Atomic($"Transformed: {g.Description}")));

        // Act
        var bound = await result.BindAsync(binder);

        // Assert
        bound.IsSuccess.Should().BeTrue();
        bound.Value.Description.Should().Be("Transformed: Original");
    }

    [Fact]
    public async Task BindAsync_FailureResult_PropagatesError()
    {
        // Arrange
        var result = Result<Goal>.Failure("error");
        Func<Goal, Task<Result<Goal>>> binder = g =>
            Task.FromResult(Result<Goal>.Success(g));

        // Act
        var bound = await result.BindAsync(binder);

        // Assert
        bound.IsFailure.Should().BeTrue();
        bound.Error.Should().Be("error");
    }

    [Fact]
    public async Task BindAsync_NullBinder_ThrowsArgumentNullException()
    {
        // Arrange
        var result = Result<Goal>.Success(Goal.Atomic("Test"));

        // Act
        var act = () => result.BindAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region BindAsync (Task<Result<Goal>>)

    [Fact]
    public async Task BindAsync_TaskResult_SuccessResult_AppliesBinder()
    {
        // Arrange
        var goal = Goal.Atomic("Original");
        var taskResult = Task.FromResult(Result<Goal>.Success(goal));
        Func<Goal, Task<Result<Goal>>> binder = g =>
            Task.FromResult(Result<Goal>.Success(Goal.Atomic($"Modified: {g.Description}")));

        // Act
        var bound = await taskResult.BindAsync(binder);

        // Assert
        bound.IsSuccess.Should().BeTrue();
        bound.Value.Description.Should().Be("Modified: Original");
    }

    [Fact]
    public async Task BindAsync_TaskResult_FailureResult_PropagatesError()
    {
        // Arrange
        var taskResult = Task.FromResult(Result<Goal>.Failure("task error"));
        Func<Goal, Task<Result<Goal>>> binder = g =>
            Task.FromResult(Result<Goal>.Success(g));

        // Act
        var bound = await taskResult.BindAsync(binder);

        // Assert
        bound.IsFailure.Should().BeTrue();
        bound.Error.Should().Be("task error");
    }

    #endregion

    #region Map

    [Fact]
    public void Map_SuccessResult_AppliesMapper()
    {
        // Arrange
        var goal = Goal.Atomic("Original");
        var result = Result<Goal>.Success(goal);

        // Act
        var mapped = result.Map(g => g.WithSubGoals(Goal.Atomic("Sub")));

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.SubGoals.Should().HaveCount(1);
    }

    [Fact]
    public void Map_FailureResult_PropagatesError()
    {
        // Arrange
        var result = Result<Goal>.Failure("error");

        // Act
        var mapped = result.Map(g => g.WithSubGoals(Goal.Atomic("Sub")));

        // Assert
        mapped.IsFailure.Should().BeTrue();
        mapped.Error.Should().Be("error");
    }

    [Fact]
    public void Map_NullMapper_ThrowsArgumentNullException()
    {
        // Arrange
        var result = Result<Goal>.Success(Goal.Atomic("Test"));

        // Act
        var act = () => result.Map(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Flatten

    [Fact]
    public void Flatten_AtomicGoal_ReturnsSingleGoal()
    {
        // Arrange
        var goal = Goal.Atomic("Leaf");

        // Act
        var flattened = goal.Flatten().ToList();

        // Assert
        flattened.Should().HaveCount(1);
        flattened[0].Description.Should().Be("Leaf");
    }

    [Fact]
    public void Flatten_HierarchicalGoal_ReturnsAllGoals()
    {
        // Arrange
        var sub1 = Goal.Atomic("Sub1");
        var sub2 = Goal.Atomic("Sub2");
        var subsub = Goal.Atomic("SubSub");
        var sub1WithChildren = sub1.WithSubGoals(subsub);
        var root = Goal.Atomic("Root").WithSubGoals(sub1WithChildren, sub2);

        // Act
        var flattened = root.Flatten().ToList();

        // Assert
        flattened.Should().HaveCount(4);
        flattened.Select(g => g.Description).Should().ContainInOrder("Root", "Sub1", "SubSub", "Sub2");
    }

    [Fact]
    public void Flatten_NullGoal_ThrowsArgumentNullException()
    {
        // Act
        var act = () => GoalExtensions.Flatten(null!).ToList();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region TotalCount

    [Fact]
    public void TotalCount_AtomicGoal_ReturnsOne()
    {
        // Arrange
        var goal = Goal.Atomic("Single");

        // Act & Assert
        goal.TotalCount().Should().Be(1);
    }

    [Fact]
    public void TotalCount_HierarchicalGoal_ReturnsCorrectCount()
    {
        // Arrange
        var sub1 = Goal.Atomic("Sub1").WithSubGoals(Goal.Atomic("SubSub1"));
        var sub2 = Goal.Atomic("Sub2");
        var root = Goal.Atomic("Root").WithSubGoals(sub1, sub2);

        // Act & Assert
        root.TotalCount().Should().Be(4);
    }

    #endregion

    #region CompletedCount

    [Fact]
    public void CompletedCount_AllComplete_ReturnsTotal()
    {
        // Arrange
        var sub1 = Goal.Atomic("Sub1", _ => true);
        var sub2 = Goal.Atomic("Sub2", _ => true);
        var root = Goal.Atomic("Root", _ => true).WithSubGoals(sub1, sub2);
        var branch = CreateBranch();

        // Act
        // Root's criteria is ignored since it has sub-goals; sub-goals are complete
        // Root is complete (all sub-goals complete), sub1 and sub2 are complete
        var count = root.CompletedCount(branch);

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public void CompletedCount_NoneComplete_ReturnsZero()
    {
        // Arrange
        var sub1 = Goal.Atomic("Sub1", _ => false);
        var sub2 = Goal.Atomic("Sub2", _ => false);
        var root = Goal.Atomic("Root").WithSubGoals(sub1, sub2);
        var branch = CreateBranch();

        // Act
        var count = root.CompletedCount(branch);

        // Assert
        count.Should().Be(0);
    }

    #endregion

    #region Progress

    [Fact]
    public void Progress_AllComplete_ReturnsOne()
    {
        // Arrange
        var sub1 = Goal.Atomic("Sub1", _ => true);
        var sub2 = Goal.Atomic("Sub2", _ => true);
        var root = Goal.Atomic("Root").WithSubGoals(sub1, sub2);
        var branch = CreateBranch();

        // Act
        var progress = root.Progress(branch);

        // Assert
        progress.Should().Be(1.0);
    }

    [Fact]
    public void Progress_NoneComplete_ReturnsZero()
    {
        // Arrange
        var sub1 = Goal.Atomic("Sub1", _ => false);
        var sub2 = Goal.Atomic("Sub2", _ => false);
        var root = Goal.Atomic("Root").WithSubGoals(sub1, sub2);
        var branch = CreateBranch();

        // Act
        var progress = root.Progress(branch);

        // Assert
        progress.Should().Be(0.0);
    }

    [Fact]
    public void Progress_HalfComplete_ReturnsCorrectPercentage()
    {
        // Arrange
        var sub1 = Goal.Atomic("Sub1", _ => true);
        var sub2 = Goal.Atomic("Sub2", _ => false);
        var branch = CreateBranch();
        // Root has 2 sub-goals: root itself is not complete (not all subs complete), sub1 complete, sub2 not
        // Total = 3 (root + 2 subs), Completed = 1 (sub1 only)
        var root = Goal.Atomic("Root").WithSubGoals(sub1, sub2);

        // Act
        var progress = root.Progress(branch);

        // Assert - root: incomplete (not all subs complete), sub1: complete, sub2: incomplete -> 1/3
        progress.Should().BeApproximately(1.0 / 3.0, 0.001);
    }

    #endregion

    #region GetLeafGoals

    [Fact]
    public void GetLeafGoals_AtomicGoal_ReturnsSelf()
    {
        // Arrange
        var goal = Goal.Atomic("Leaf");

        // Act
        var leaves = goal.GetLeafGoals().ToList();

        // Assert
        leaves.Should().HaveCount(1);
        leaves[0].Description.Should().Be("Leaf");
    }

    [Fact]
    public void GetLeafGoals_HierarchicalGoal_ReturnsOnlyLeaves()
    {
        // Arrange
        var leaf1 = Goal.Atomic("Leaf1");
        var leaf2 = Goal.Atomic("Leaf2");
        var parent = Goal.Atomic("Parent").WithSubGoals(leaf1, leaf2);
        var root = Goal.Atomic("Root").WithSubGoals(parent);

        // Act
        var leaves = root.GetLeafGoals().ToList();

        // Assert
        leaves.Should().HaveCount(2);
        leaves.Select(g => g.Description).Should().Contain("Leaf1");
        leaves.Select(g => g.Description).Should().Contain("Leaf2");
    }

    #endregion

    #region MaxDepth

    [Fact]
    public void MaxDepth_AtomicGoal_ReturnsOne()
    {
        // Arrange
        var goal = Goal.Atomic("Leaf");

        // Act & Assert
        goal.MaxDepth().Should().Be(1);
    }

    [Fact]
    public void MaxDepth_TwoLevels_ReturnsTwo()
    {
        // Arrange
        var sub = Goal.Atomic("Sub");
        var root = Goal.Atomic("Root").WithSubGoals(sub);

        // Act & Assert
        root.MaxDepth().Should().Be(2);
    }

    [Fact]
    public void MaxDepth_AsymmetricTree_ReturnsMaxBranch()
    {
        // Arrange
        var deep = Goal.Atomic("Deep");
        var mid = Goal.Atomic("Mid").WithSubGoals(deep);
        var shallow = Goal.Atomic("Shallow");
        var root = Goal.Atomic("Root").WithSubGoals(mid, shallow);

        // Act & Assert
        root.MaxDepth().Should().Be(3);
    }

    #endregion

    #region ToTreeString

    [Fact]
    public void ToTreeString_AtomicGoal_ReturnsFormattedString()
    {
        // Arrange
        var goal = Goal.Atomic("My Goal");

        // Act
        var result = goal.ToTreeString();

        // Assert
        result.Should().Contain("- My Goal");
    }

    [Fact]
    public void ToTreeString_WithSubGoals_ReturnsIndentedString()
    {
        // Arrange
        var sub = Goal.Atomic("Sub Goal");
        var root = Goal.Atomic("Root Goal").WithSubGoals(sub);

        // Act
        var result = root.ToTreeString();

        // Assert
        result.Should().Contain("- Root Goal");
        result.Should().Contain("  - Sub Goal");
    }

    #endregion

    #region Filter

    [Fact]
    public void Filter_PredicateMatchesRoot_ReturnsSome()
    {
        // Arrange
        var goal = Goal.Atomic("Keep this");

        // Act
        var filtered = goal.Filter(_ => true);

        // Assert
        filtered.HasValue.Should().BeTrue();
    }

    [Fact]
    public void Filter_PredicateExcludesRoot_ReturnsNone()
    {
        // Arrange
        var goal = Goal.Atomic("Exclude this");

        // Act
        var filtered = goal.Filter(_ => false);

        // Assert
        filtered.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Filter_PredicateExcludesSomeSubGoals_FiltersSubGoals()
    {
        // Arrange
        var sub1 = Goal.Atomic("Keep");
        var sub2 = Goal.Atomic("Remove");
        var root = Goal.Atomic("Root").WithSubGoals(sub1, sub2);

        // Act
        var filtered = root.Filter(g => g.Description != "Remove");

        // Assert
        filtered.HasValue.Should().BeTrue();
        filtered.Value!.SubGoals.Should().HaveCount(1);
        filtered.Value.SubGoals[0].Description.Should().Be("Keep");
    }

    [Fact]
    public void Filter_NullGoal_ThrowsArgumentNullException()
    {
        // Act
        var act = () => GoalExtensions.Filter(null!, _ => true);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Filter_NullPredicate_ThrowsArgumentNullException()
    {
        // Arrange
        var goal = Goal.Atomic("Test");

        // Act
        var act = () => goal.Filter(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion
}
