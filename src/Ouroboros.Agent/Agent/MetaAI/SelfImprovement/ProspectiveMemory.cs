// ==========================================================
// Prospective Memory
// Einstein & McDaniel — time-based and event-based
// future-oriented remembering
// ==========================================================

using System.Collections.Concurrent;

namespace Ouroboros.Agent.MetaAI.SelfImprovement;

/// <summary>
/// Type of prospective memory reminder trigger.
/// </summary>
public enum ReminderType
{
    /// <summary>Triggers at a specific time.</summary>
    TimeBased,

    /// <summary>Triggers when a matching event or context is encountered.</summary>
    EventBased
}

/// <summary>
/// A prospective memory reminder with its trigger condition and action.
/// </summary>
/// <param name="Id">Unique reminder identifier.</param>
/// <param name="Description">Human-readable description of the reminder.</param>
/// <param name="Type">Whether the reminder is time-based or event-based.</param>
/// <param name="TriggerCondition">For event-based: the context substring that triggers it.</param>
/// <param name="Action">The action to perform when triggered.</param>
/// <param name="CreatedAt">When the reminder was created.</param>
/// <param name="TriggerTime">For time-based: when the reminder should fire.</param>
/// <param name="IsTriggered">Whether the reminder has already been triggered.</param>
/// <param name="IsCompleted">Whether the reminder action has been completed.</param>
/// <param name="IsCancelled">Whether the reminder has been cancelled.</param>
public sealed record ProspectiveReminder(
    string Id,
    string Description,
    ReminderType Type,
    string? TriggerCondition,
    string Action,
    DateTime CreatedAt,
    DateTime? TriggerTime,
    bool IsTriggered,
    bool IsCompleted,
    bool IsCancelled);

/// <summary>
/// Implements Einstein and McDaniel's model of prospective memory —
/// the ability to remember to perform intended actions at future points.
/// Supports both time-based and event-based reminders with a maximum
/// of 100 active reminders.
/// </summary>
public sealed class ProspectiveMemory
{
    private const int MaxActiveReminders = 100;
    private readonly ConcurrentDictionary<string, ProspectiveReminder> _reminders = new();

    /// <summary>
    /// Creates a time-based reminder that triggers at a specified time.
    /// </summary>
    /// <param name="description">Description of what to remember.</param>
    /// <param name="triggerTime">When the reminder should fire.</param>
    /// <param name="action">The action to perform when triggered.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created reminder.</returns>
    public Task<ProspectiveReminder> CreateTimeBasedReminderAsync(
        string description,
        DateTime triggerTime,
        string action,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(action);
        ct.ThrowIfCancellationRequested();

        EnforceCapacity();

        var reminder = new ProspectiveReminder(
            Id: Guid.NewGuid().ToString("N"),
            Description: description,
            Type: ReminderType.TimeBased,
            TriggerCondition: null,
            Action: action,
            CreatedAt: DateTime.UtcNow,
            TriggerTime: triggerTime,
            IsTriggered: false,
            IsCompleted: false,
            IsCancelled: false);

        _reminders.TryAdd(reminder.Id, reminder);
        return Task.FromResult(reminder);
    }

    /// <summary>
    /// Creates an event-based reminder that triggers when a matching context is encountered.
    /// </summary>
    /// <param name="description">Description of what to remember.</param>
    /// <param name="triggerCondition">Context substring that triggers the reminder.</param>
    /// <param name="action">The action to perform when triggered.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created reminder.</returns>
    public Task<ProspectiveReminder> CreateEventBasedReminderAsync(
        string description,
        string triggerCondition,
        string action,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(triggerCondition);
        ArgumentNullException.ThrowIfNull(action);
        ct.ThrowIfCancellationRequested();

        EnforceCapacity();

        var reminder = new ProspectiveReminder(
            Id: Guid.NewGuid().ToString("N"),
            Description: description,
            Type: ReminderType.EventBased,
            TriggerCondition: triggerCondition,
            Action: action,
            CreatedAt: DateTime.UtcNow,
            TriggerTime: null,
            IsTriggered: false,
            IsCompleted: false,
            IsCancelled: false);

        _reminders.TryAdd(reminder.Id, reminder);
        return Task.FromResult(reminder);
    }

    /// <summary>
    /// Checks all active reminders against the current context and time,
    /// returning any that should be triggered.
    /// </summary>
    /// <param name="currentContext">Current context string to match event-based reminders.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of newly triggered reminders.</returns>
    public Task<List<ProspectiveReminder>> CheckTriggeredRemindersAsync(
        string currentContext,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var triggered = new List<ProspectiveReminder>();
        var now = DateTime.UtcNow;

        foreach (var kvp in _reminders)
        {
            var r = kvp.Value;
            if (r.IsTriggered || r.IsCompleted || r.IsCancelled)
                continue;

            bool shouldTrigger = r.Type switch
            {
                ReminderType.TimeBased => r.TriggerTime.HasValue && now >= r.TriggerTime.Value,
                ReminderType.EventBased => !string.IsNullOrEmpty(r.TriggerCondition)
                    && !string.IsNullOrEmpty(currentContext)
                    && currentContext.Contains(r.TriggerCondition, StringComparison.OrdinalIgnoreCase),
                _ => false
            };

            if (shouldTrigger)
            {
                var updated = r with { IsTriggered = true };
                _reminders.TryUpdate(kvp.Key, updated, r);
                triggered.Add(updated);
            }
        }

        return Task.FromResult(triggered);
    }

    /// <summary>
    /// Marks a reminder as completed.
    /// </summary>
    /// <param name="id">Reminder identifier.</param>
    /// <returns>True if the reminder was found and marked complete.</returns>
    public bool MarkReminderComplete(string id)
    {
        if (_reminders.TryGetValue(id, out var r) && !r.IsCancelled)
        {
            return _reminders.TryUpdate(id, r with { IsCompleted = true, IsTriggered = true }, r);
        }
        return false;
    }

    /// <summary>
    /// Cancels an active reminder.
    /// </summary>
    /// <param name="id">Reminder identifier.</param>
    /// <returns>True if the reminder was found and cancelled.</returns>
    public bool CancelReminder(string id)
    {
        if (_reminders.TryGetValue(id, out var r) && !r.IsCompleted)
        {
            return _reminders.TryUpdate(id, r with { IsCancelled = true }, r);
        }
        return false;
    }

    /// <summary>
    /// Returns all pending (not triggered, not completed, not cancelled) reminders.
    /// </summary>
    public IReadOnlyList<ProspectiveReminder> GetPendingReminders()
    {
        return _reminders.Values
            .Where(r => !r.IsTriggered && !r.IsCompleted && !r.IsCancelled)
            .OrderBy(r => r.TriggerTime ?? DateTime.MaxValue)
            .ToList();
    }

    /// <summary>
    /// Returns the total number of reminders (including completed and cancelled).
    /// </summary>
    public int TotalReminders => _reminders.Count;

    private void EnforceCapacity()
    {
        int activeCount = _reminders.Values.Count(r => !r.IsCompleted && !r.IsCancelled);
        if (activeCount >= MaxActiveReminders)
        {
            // Remove oldest completed/cancelled reminders first
            var toRemove = _reminders
                .Where(kvp => kvp.Value.IsCompleted || kvp.Value.IsCancelled)
                .OrderBy(kvp => kvp.Value.CreatedAt)
                .Take(10)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (string key in toRemove)
                _reminders.TryRemove(key, out _);

            // If still over capacity, reject
            activeCount = _reminders.Values.Count(r => !r.IsCompleted && !r.IsCancelled);
            if (activeCount >= MaxActiveReminders)
                throw new InvalidOperationException(
                    $"Maximum active reminders ({MaxActiveReminders}) reached. Cancel or complete existing reminders first.");
        }
    }
}
