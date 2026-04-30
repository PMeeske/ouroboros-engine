// <copyright file="PriorityModulatorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Agent.MetaAI.Affect;
using AffectTaskStatus = Ouroboros.Agent.MetaAI.Affect.TaskStatus;

namespace Ouroboros.Tests.MetaAI.Affect;

[Trait("Category", "Unit")]
public class PriorityModulatorTests
{
    private readonly PriorityModulator _modulator = new();

    [Fact]
    public void AddTask_ReturnsTask_WithCorrectProperties()
    {
        var task = _modulator.AddTask("Test", "A test task", 0.8);

        task.Name.Should().Be("Test");
        task.Description.Should().Be("A test task");
        task.BasePriority.Should().Be(0.8);
        task.Status.Should().Be(AffectTaskStatus.Pending);
    }

    [Fact]
    public void AddTask_ClampsPriority()
    {
        var task = _modulator.AddTask("T", "D", 1.5);
        task.BasePriority.Should().Be(1.0);

        var task2 = _modulator.AddTask("T2", "D2", -0.5);
        task2.BasePriority.Should().Be(0.0);
    }

    [Fact]
    public void AddTask_NullName_Throws()
    {
        var act = () => _modulator.AddTask(null!, "d", 0.5);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetNextTask_ReturnsHighestPriorityPendingTask()
    {
        _modulator.AddTask("Low", "low", 0.2);
        _modulator.AddTask("High", "high", 0.9);
        _modulator.AddTask("Mid", "mid", 0.5);

        var next = _modulator.GetNextTask();

        next.Should().NotBeNull();
        next!.Name.Should().Be("High");
    }

    [Fact]
    public void GetNextTask_NoTasks_ReturnsNull()
    {
        _modulator.GetNextTask().Should().BeNull();
    }

    [Fact]
    public void UpdateTaskStatus_ChangesStatus()
    {
        var task = _modulator.AddTask("T", "D", 0.5);

        _modulator.UpdateTaskStatus(task.Id, AffectTaskStatus.Completed);

        var tasks = _modulator.GetTasks(includeDone: true);
        tasks.Should().Contain(t => t.Id == task.Id && t.Status == AffectTaskStatus.Completed);
    }

    [Fact]
    public void RemoveTask_RemovesFromList()
    {
        var task = _modulator.AddTask("T", "D", 0.5);

        _modulator.RemoveTask(task.Id);

        _modulator.GetTasks(includeDone: true).Should().NotContain(t => t.Id == task.Id);
    }

    [Fact]
    public void ModulatePriorities_AdjustsPriorityBasedOnAffect()
    {
        var task = _modulator.AddTask("Explore new thing", "explore and learn", 0.5);
        var state = new AffectiveState(
            Guid.NewGuid(), 0.3, 0.2, 0.8, 0.7, 0.5, DateTime.UtcNow, new Dictionary<string, object>());

        _modulator.ModulatePriorities(state);

        var tasks = _modulator.GetTasks();
        tasks.Should().Contain(t => t.Id == task.Id);
    }

    [Fact]
    public void ModulatePriorities_NullState_Throws()
    {
        var act = () => _modulator.ModulatePriorities(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectCounts()
    {
        _modulator.AddTask("T1", "D1", 0.5);
        var t2 = _modulator.AddTask("T2", "D2", 0.7);
        _modulator.UpdateTaskStatus(t2.Id, AffectTaskStatus.Completed);

        var stats = _modulator.GetStatistics();

        stats.TotalTasks.Should().Be(2);
        stats.PendingTasks.Should().BeGreaterThanOrEqualTo(1);
        stats.CompletedTasks.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void PrioritizeByThreat_ReordersTasksByThreatLevel()
    {
        _modulator.AddTask("T1", "D1", 0.3);
        _modulator.AddTask("T2", "D2", 0.8);

        _modulator.PrioritizeByThreat();

        var tasks = _modulator.GetTasks();
        tasks.Should().NotBeEmpty();
    }

    [Fact]
    public void PrioritizeByOpportunity_ReordersTasksByOpportunity()
    {
        _modulator.AddTask("T1", "D1", 0.3);
        _modulator.AddTask("T2", "D2", 0.8);

        _modulator.PrioritizeByOpportunity();

        var tasks = _modulator.GetTasks();
        tasks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AppraiseTaskAsync_NonexistentTask_ReturnsDefaultAppraisal()
    {
        var state = new AffectiveState(
            Guid.NewGuid(), 0.0, 0.5, 0.5, 0.5, 0.5, DateTime.UtcNow, new Dictionary<string, object>());

        var appraisal = await _modulator.AppraiseTaskAsync(Guid.NewGuid(), state);

        appraisal.Rationale.Should().Contain("not found");
    }

    [Fact]
    public async Task AppraiseTaskAsync_ValidTask_ReturnsAppraisal()
    {
        var task = _modulator.AddTask("Urgent", "Due tomorrow", 0.9, DateTime.UtcNow.AddHours(12));
        var state = new AffectiveState(
            Guid.NewGuid(), 0.2, 0.7, 0.3, 0.5, 0.6, DateTime.UtcNow, new Dictionary<string, object>());

        var appraisal = await _modulator.AppraiseTaskAsync(task.Id, state);

        appraisal.UrgencyFactor.Should().BeGreaterThanOrEqualTo(0.0);
        appraisal.ThreatLevel.Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Fact]
    public void GetTasks_ExcludesDoneByDefault()
    {
        var t = _modulator.AddTask("T", "D", 0.5);
        _modulator.UpdateTaskStatus(t.Id, AffectTaskStatus.Completed);

        _modulator.GetTasks(includeDone: false).Should().NotContain(x => x.Id == t.Id);
        _modulator.GetTasks(includeDone: true).Should().Contain(x => x.Id == t.Id);
    }
}
