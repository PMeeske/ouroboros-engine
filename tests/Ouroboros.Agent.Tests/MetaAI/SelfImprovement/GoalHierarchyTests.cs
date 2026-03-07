// <copyright file="GoalHierarchyTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class GoalHierarchyTests
{
    private readonly Mock<IChatCompletionModel> _llmMock = new();
    private readonly Mock<ISafetyGuard> _safetyMock = new();
    private readonly Mock<Ouroboros.Core.Ethics.IEthicsFramework> _ethicsMock = new();
    private readonly GoalHierarchy _hierarchy;

    public GoalHierarchyTests()
    {
        _hierarchy = new GoalHierarchy(_llmMock.Object, _safetyMock.Object, _ethicsMock.Object);
    }

    [Fact]
    public void Constructor_NullLlm_Throws()
    {
        var act = () => new GoalHierarchy(null!, _safetyMock.Object, _ethicsMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddGoal_AddsToHierarchy()
    {
        var goal = CreateGoal("Test goal");

        _hierarchy.AddGoal(goal);

        _hierarchy.GetGoal(goal.Id).Should().NotBeNull();
    }

    [Fact]
    public void AddGoal_Null_Throws()
    {
        var act = () => _hierarchy.AddGoal(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetGoal_NotFound_ReturnsNull()
    {
        _hierarchy.GetGoal(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void GetActiveGoals_ExcludesCompleted()
    {
        var g1 = CreateGoal("Active");
        var g2 = CreateGoal("Done", isComplete: true);

        _hierarchy.AddGoal(g1);
        _hierarchy.AddGoal(g2);

        _hierarchy.GetActiveGoals().Should().HaveCount(1);
        _hierarchy.GetActiveGoals()[0].Description.Should().Be("Active");
    }

    [Fact]
    public void CompleteGoal_MarksComplete()
    {
        var goal = CreateGoal("Test");
        _hierarchy.AddGoal(goal);

        _hierarchy.CompleteGoal(goal.Id, "Done successfully");

        var completed = _hierarchy.GetGoal(goal.Id);
        completed!.IsComplete.Should().BeTrue();
        completed.CompletionReason.Should().Be("Done successfully");
    }

    [Fact]
    public void GetGoalTree_ReturnsRootGoals()
    {
        var root = CreateGoal("Root", priority: 0.9);
        _hierarchy.AddGoal(root);

        var tree = _hierarchy.GetGoalTree();

        tree.Should().HaveCount(1);
        tree[0].Description.Should().Be("Root");
    }

    [Fact]
    public async Task PrioritizeGoalsAsync_ReturnsSortedGoals()
    {
        var safety = CreateGoal("Safety", type: GoalType.Safety, priority: 0.8);
        var primary = CreateGoal("Primary", type: GoalType.Primary, priority: 0.9);
        var secondary = CreateGoal("Secondary", type: GoalType.Secondary, priority: 0.5);

        _hierarchy.AddGoal(safety);
        _hierarchy.AddGoal(primary);
        _hierarchy.AddGoal(secondary);

        var prioritized = await _hierarchy.PrioritizeGoalsAsync();

        prioritized.Should().HaveCount(3);
        // Safety goals should come first
        prioritized[0].Type.Should().Be(GoalType.Safety);
    }

    [Fact]
    public async Task AddGoalAsync_NullGoal_ReturnsFailure()
    {
        var result = await _hierarchy.AddGoalAsync(null!);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null");
    }

    [Fact]
    public async Task CheckValueAlignmentAsync_NullGoal_ReturnsFailure()
    {
        var result = await _hierarchy.CheckValueAlignmentAsync(null!);

        result.IsFailure.Should().BeTrue();
    }

    private static Ouroboros.Agent.MetaAI.Goal CreateGoal(
        string description,
        GoalType type = GoalType.Primary,
        double priority = 0.5,
        bool isComplete = false)
    {
        return new Ouroboros.Agent.MetaAI.Goal(
            Guid.NewGuid(),
            description,
            type,
            priority,
            null,
            new List<Ouroboros.Agent.MetaAI.Goal>(),
            new Dictionary<string, object>(),
            DateTime.UtcNow,
            isComplete,
            null);
    }
}
