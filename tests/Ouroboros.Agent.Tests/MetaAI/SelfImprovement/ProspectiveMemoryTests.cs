// <copyright file="ProspectiveMemoryTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfImprovement;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class ProspectiveMemoryTests
{
    // ── CreateTimeBasedReminderAsync ─────────────────────────────────

    [Fact]
    public async Task CreateTimeBasedReminderAsync_NullDescription_Throws()
    {
        var memory = new ProspectiveMemory();
        var act = () => memory.CreateTimeBasedReminderAsync(null!, DateTime.UtcNow.AddHours(1), "action");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateTimeBasedReminderAsync_NullAction_Throws()
    {
        var memory = new ProspectiveMemory();
        var act = () => memory.CreateTimeBasedReminderAsync("desc", DateTime.UtcNow.AddHours(1), null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateTimeBasedReminderAsync_CancelledToken_Throws()
    {
        var memory = new ProspectiveMemory();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => memory.CreateTimeBasedReminderAsync("desc", DateTime.UtcNow.AddHours(1), "action", cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CreateTimeBasedReminderAsync_ValidInputs_CreatesReminder()
    {
        // Arrange
        var memory = new ProspectiveMemory();
        var triggerTime = DateTime.UtcNow.AddHours(1);

        // Act
        var reminder = await memory.CreateTimeBasedReminderAsync("Check status", triggerTime, "run check");

        // Assert
        reminder.Description.Should().Be("Check status");
        reminder.Type.Should().Be(ReminderType.TimeBased);
        reminder.TriggerTime.Should().Be(triggerTime);
        reminder.Action.Should().Be("run check");
        reminder.IsTriggered.Should().BeFalse();
        reminder.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateTimeBasedReminderAsync_IncrementsTotalReminders()
    {
        // Arrange
        var memory = new ProspectiveMemory();

        // Act
        await memory.CreateTimeBasedReminderAsync("r1", DateTime.UtcNow.AddHours(1), "a1");
        await memory.CreateTimeBasedReminderAsync("r2", DateTime.UtcNow.AddHours(2), "a2");

        // Assert
        memory.TotalReminders.Should().Be(2);
    }

    // ── CreateEventBasedReminderAsync ────────────────────────────────

    [Fact]
    public async Task CreateEventBasedReminderAsync_NullDescription_Throws()
    {
        var memory = new ProspectiveMemory();
        var act = () => memory.CreateEventBasedReminderAsync(null!, "condition", "action");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateEventBasedReminderAsync_NullTriggerCondition_Throws()
    {
        var memory = new ProspectiveMemory();
        var act = () => memory.CreateEventBasedReminderAsync("desc", null!, "action");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateEventBasedReminderAsync_NullAction_Throws()
    {
        var memory = new ProspectiveMemory();
        var act = () => memory.CreateEventBasedReminderAsync("desc", "condition", null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateEventBasedReminderAsync_ValidInputs_CreatesReminder()
    {
        // Arrange
        var memory = new ProspectiveMemory();

        // Act
        var reminder = await memory.CreateEventBasedReminderAsync(
            "When meeting starts", "meeting started", "take notes");

        // Assert
        reminder.Description.Should().Be("When meeting starts");
        reminder.Type.Should().Be(ReminderType.EventBased);
        reminder.TriggerCondition.Should().Be("meeting started");
        reminder.Action.Should().Be("take notes");
        reminder.IsTriggered.Should().BeFalse();
        reminder.TriggerTime.Should().BeNull();
    }

    // ── CheckTriggeredRemindersAsync ────────────────────────────────

    [Fact]
    public async Task CheckTriggeredRemindersAsync_TimeBasedReminderPastDue_Triggers()
    {
        // Arrange
        var memory = new ProspectiveMemory();
        await memory.CreateTimeBasedReminderAsync(
            "Past due", DateTime.UtcNow.AddHours(-1), "action");

        // Act
        var triggered = await memory.CheckTriggeredRemindersAsync("any context");

        // Assert
        triggered.Should().HaveCount(1);
        triggered[0].IsTriggered.Should().BeTrue();
        triggered[0].Description.Should().Be("Past due");
    }

    [Fact]
    public async Task CheckTriggeredRemindersAsync_TimeBasedReminderFuture_DoesNotTrigger()
    {
        // Arrange
        var memory = new ProspectiveMemory();
        await memory.CreateTimeBasedReminderAsync(
            "Future", DateTime.UtcNow.AddHours(1), "action");

        // Act
        var triggered = await memory.CheckTriggeredRemindersAsync("any context");

        // Assert
        triggered.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckTriggeredRemindersAsync_EventBasedReminderMatchingContext_Triggers()
    {
        // Arrange
        var memory = new ProspectiveMemory();
        await memory.CreateEventBasedReminderAsync(
            "Deploy reminder", "deployment", "run smoke tests");

        // Act
        var triggered = await memory.CheckTriggeredRemindersAsync("Starting deployment process");

        // Assert
        triggered.Should().HaveCount(1);
        triggered[0].Description.Should().Be("Deploy reminder");
    }

    [Fact]
    public async Task CheckTriggeredRemindersAsync_EventBasedReminderNoMatch_DoesNotTrigger()
    {
        // Arrange
        var memory = new ProspectiveMemory();
        await memory.CreateEventBasedReminderAsync(
            "Deploy reminder", "deployment", "run smoke tests");

        // Act
        var triggered = await memory.CheckTriggeredRemindersAsync("Working on code review");

        // Assert
        triggered.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckTriggeredRemindersAsync_AlreadyTriggered_DoesNotTriggerAgain()
    {
        // Arrange
        var memory = new ProspectiveMemory();
        await memory.CreateEventBasedReminderAsync("once", "trigger", "action");

        // Act — trigger once
        var first = await memory.CheckTriggeredRemindersAsync("trigger event");
        first.Should().HaveCount(1);

        // Act — check again
        var second = await memory.CheckTriggeredRemindersAsync("trigger event");

        // Assert
        second.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckTriggeredRemindersAsync_CompletedReminder_DoesNotTrigger()
    {
        // Arrange
        var memory = new ProspectiveMemory();
        var reminder = await memory.CreateTimeBasedReminderAsync(
            "completed", DateTime.UtcNow.AddHours(-1), "action");
        memory.MarkReminderComplete(reminder.Id);

        // Act
        var triggered = await memory.CheckTriggeredRemindersAsync("any context");

        // Assert
        triggered.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckTriggeredRemindersAsync_CancelledReminder_DoesNotTrigger()
    {
        // Arrange
        var memory = new ProspectiveMemory();
        var reminder = await memory.CreateTimeBasedReminderAsync(
            "cancelled", DateTime.UtcNow.AddHours(-1), "action");
        memory.CancelReminder(reminder.Id);

        // Act
        var triggered = await memory.CheckTriggeredRemindersAsync("any context");

        // Assert
        triggered.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckTriggeredRemindersAsync_CaseInsensitiveEventMatch()
    {
        // Arrange
        var memory = new ProspectiveMemory();
        await memory.CreateEventBasedReminderAsync("test", "DEPLOY", "action");

        // Act
        var triggered = await memory.CheckTriggeredRemindersAsync("starting deploy now");

        // Assert
        triggered.Should().HaveCount(1);
    }

    // ── MarkReminderComplete ────────────────────────────────────────

    [Fact]
    public async Task MarkReminderComplete_ValidId_ReturnsTrue()
    {
        // Arrange
        var memory = new ProspectiveMemory();
        var reminder = await memory.CreateTimeBasedReminderAsync("r", DateTime.UtcNow, "a");

        // Act
        var result = memory.MarkReminderComplete(reminder.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MarkReminderComplete_UnknownId_ReturnsFalse()
    {
        // Arrange
        var memory = new ProspectiveMemory();

        // Act
        var result = memory.MarkReminderComplete("unknown-id");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task MarkReminderComplete_CancelledReminder_ReturnsFalse()
    {
        // Arrange
        var memory = new ProspectiveMemory();
        var reminder = await memory.CreateTimeBasedReminderAsync("r", DateTime.UtcNow, "a");
        memory.CancelReminder(reminder.Id);

        // Act
        var result = memory.MarkReminderComplete(reminder.Id);

        // Assert
        result.Should().BeFalse();
    }

    // ── CancelReminder ──────────────────────────────────────────────

    [Fact]
    public async Task CancelReminder_ValidId_ReturnsTrue()
    {
        // Arrange
        var memory = new ProspectiveMemory();
        var reminder = await memory.CreateTimeBasedReminderAsync("r", DateTime.UtcNow, "a");

        // Act
        var result = memory.CancelReminder(reminder.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CancelReminder_UnknownId_ReturnsFalse()
    {
        // Arrange
        var memory = new ProspectiveMemory();

        // Act
        var result = memory.CancelReminder("unknown-id");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CancelReminder_AlreadyCompleted_ReturnsFalse()
    {
        // Arrange
        var memory = new ProspectiveMemory();
        var reminder = await memory.CreateTimeBasedReminderAsync("r", DateTime.UtcNow, "a");
        memory.MarkReminderComplete(reminder.Id);

        // Act
        var result = memory.CancelReminder(reminder.Id);

        // Assert
        result.Should().BeFalse();
    }

    // ── GetPendingReminders ─────────────────────────────────────────

    [Fact]
    public void GetPendingReminders_Empty_ReturnsEmpty()
    {
        var memory = new ProspectiveMemory();
        memory.GetPendingReminders().Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingReminders_ExcludesTriggeredCompletedCancelled()
    {
        // Arrange
        var memory = new ProspectiveMemory();
        var r1 = await memory.CreateTimeBasedReminderAsync("pending", DateTime.UtcNow.AddHours(1), "a1");
        var r2 = await memory.CreateTimeBasedReminderAsync("completed", DateTime.UtcNow.AddHours(2), "a2");
        var r3 = await memory.CreateTimeBasedReminderAsync("cancelled", DateTime.UtcNow.AddHours(3), "a3");

        memory.MarkReminderComplete(r2.Id);
        memory.CancelReminder(r3.Id);

        // Act
        var pending = memory.GetPendingReminders();

        // Assert
        pending.Should().HaveCount(1);
        pending[0].Description.Should().Be("pending");
    }

    [Fact]
    public async Task GetPendingReminders_SortedByTriggerTime()
    {
        // Arrange
        var memory = new ProspectiveMemory();
        await memory.CreateTimeBasedReminderAsync("later", DateTime.UtcNow.AddHours(3), "a");
        await memory.CreateTimeBasedReminderAsync("sooner", DateTime.UtcNow.AddHours(1), "a");
        await memory.CreateTimeBasedReminderAsync("middle", DateTime.UtcNow.AddHours(2), "a");

        // Act
        var pending = memory.GetPendingReminders();

        // Assert
        pending.Should().HaveCount(3);
        pending[0].Description.Should().Be("sooner");
        pending[1].Description.Should().Be("middle");
        pending[2].Description.Should().Be("later");
    }

    // ── TotalReminders ──────────────────────────────────────────────

    [Fact]
    public void TotalReminders_InitiallyZero()
    {
        var memory = new ProspectiveMemory();
        memory.TotalReminders.Should().Be(0);
    }
}
