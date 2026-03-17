using FluentAssertions;
using NSubstitute;
using LangChain.Databases;
using Ouroboros.Pipeline.Branches;
using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Tests.Planning;

[Trait("Category", "Unit")]
public sealed class GoalTests
{
    private static PipelineBranch CreateBranch()
    {
        var store = Substitute.For<IVectorStore>();
        return new PipelineBranch("test", store, new DataSource("test", "test"));
    }

    [Fact]
    public void Atomic_WithDescription_CreatesGoalWithNoSubGoals()
    {
        // Arrange & Act
        var goal = Goal.Atomic("Test goal");

        // Assert
        goal.Description.Should().Be("Test goal");
        goal.SubGoals.Should().BeEmpty();
        goal.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Atomic_WithDescriptionAndCriteria_CreatesGoalWithCriteria()
    {
        // Arrange
        Func<PipelineBranch, bool> criteria = _ => true;

        // Act
        var goal = Goal.Atomic("Test goal", criteria);

        // Assert
        goal.Description.Should().Be("Test goal");
        goal.SubGoals.Should().BeEmpty();
    }

    [Fact]
    public void Atomic_WithNullDescription_ThrowsArgumentNullException()
    {
        // Act
        var act = () => Goal.Atomic(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Atomic_WithNullCriteria_ThrowsArgumentNullException()
    {
        // Act
        var act = () => Goal.Atomic("test", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Atomic_DefaultCriteria_AlwaysReturnsFalse()
    {
        // Arrange
        var goal = Goal.Atomic("Test goal");
        var branch = CreateBranch();

        // Act
        var isComplete = goal.IsComplete(branch);

        // Assert
        isComplete.Should().BeFalse();
    }

    [Fact]
    public void WithSubGoals_ReplacesSubGoals()
    {
        // Arrange
        var parent = Goal.Atomic("Parent");
        var sub1 = Goal.Atomic("Sub1");
        var sub2 = Goal.Atomic("Sub2");

        // Act
        var result = parent.WithSubGoals(sub1, sub2);

        // Assert
        result.SubGoals.Should().HaveCount(2);
        result.SubGoals[0].Description.Should().Be("Sub1");
        result.SubGoals[1].Description.Should().Be("Sub2");
    }

    [Fact]
    public void WithSubGoals_NullArray_ThrowsArgumentNullException()
    {
        // Arrange
        var goal = Goal.Atomic("Test");

        // Act
        var act = () => goal.WithSubGoals(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AppendSubGoals_AddsToExistingSubGoals()
    {
        // Arrange
        var sub1 = Goal.Atomic("Sub1");
        var sub2 = Goal.Atomic("Sub2");
        var sub3 = Goal.Atomic("Sub3");
        var parent = Goal.Atomic("Parent").WithSubGoals(sub1);

        // Act
        var result = parent.AppendSubGoals(sub2, sub3);

        // Assert
        result.SubGoals.Should().HaveCount(3);
    }

    [Fact]
    public void AppendSubGoals_NullArray_ThrowsArgumentNullException()
    {
        // Arrange
        var goal = Goal.Atomic("Test");

        // Act
        var act = () => goal.AppendSubGoals(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsComplete_AtomicGoalWithTrueCriteria_ReturnsTrue()
    {
        // Arrange
        var goal = Goal.Atomic("Test", _ => true);
        var branch = CreateBranch();

        // Act
        var result = goal.IsComplete(branch);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsComplete_CompositeGoal_AllSubGoalsComplete_ReturnsTrue()
    {
        // Arrange
        var sub1 = Goal.Atomic("Sub1", _ => true);
        var sub2 = Goal.Atomic("Sub2", _ => true);
        var parent = Goal.Atomic("Parent").WithSubGoals(sub1, sub2);
        var branch = CreateBranch();

        // Act
        var result = parent.IsComplete(branch);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsComplete_CompositeGoal_SomeSubGoalsIncomplete_ReturnsFalse()
    {
        // Arrange
        var sub1 = Goal.Atomic("Sub1", _ => true);
        var sub2 = Goal.Atomic("Sub2", _ => false);
        var parent = Goal.Atomic("Parent").WithSubGoals(sub1, sub2);
        var branch = CreateBranch();

        // Act
        var result = parent.IsComplete(branch);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsComplete_NullBranch_ThrowsArgumentNullException()
    {
        // Arrange
        var goal = Goal.Atomic("Test");

        // Act
        var act = () => goal.IsComplete(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetIncompleteSubGoals_ReturnsOnlyIncomplete()
    {
        // Arrange
        var sub1 = Goal.Atomic("Sub1", _ => true);
        var sub2 = Goal.Atomic("Sub2", _ => false);
        var sub3 = Goal.Atomic("Sub3", _ => false);
        var parent = Goal.Atomic("Parent").WithSubGoals(sub1, sub2, sub3);
        var branch = CreateBranch();

        // Act
        var incomplete = parent.GetIncompleteSubGoals(branch).ToList();

        // Assert
        incomplete.Should().HaveCount(2);
        incomplete.Select(g => g.Description).Should().Contain("Sub2");
        incomplete.Select(g => g.Description).Should().Contain("Sub3");
    }

    [Fact]
    public void GetIncompleteSubGoals_NullBranch_ThrowsArgumentNullException()
    {
        // Arrange
        var goal = Goal.Atomic("Test");

        // Act
        var act = () => goal.GetIncompleteSubGoals(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToOption_WithDescription_ReturnsSome()
    {
        // Arrange
        var goal = Goal.Atomic("Test goal");

        // Act
        var option = goal.ToOption();

        // Assert
        option.HasValue.Should().BeTrue();
    }

    [Fact]
    public void ToOption_EmptyDescription_ReturnsNone()
    {
        // Arrange
        var goal = new Goal(Guid.NewGuid(), "", [], _ => false);

        // Act
        var option = goal.ToOption();

        // Assert
        option.HasValue.Should().BeFalse();
    }

    [Fact]
    public void ToOption_WhitespaceDescription_ReturnsNone()
    {
        // Arrange
        var goal = new Goal(Guid.NewGuid(), "   ", [], _ => false);

        // Act
        var option = goal.ToOption();

        // Assert
        option.HasValue.Should().BeFalse();
    }

    [Fact]
    public void RecordEquality_SameId_AreConsideredEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        Func<PipelineBranch, bool> criteria = _ => false;
        var goal1 = new Goal(id, "Test", [], criteria);
        var goal2 = new Goal(id, "Test", [], criteria);

        // Assert
        goal1.Should().Be(goal2);
    }
}
