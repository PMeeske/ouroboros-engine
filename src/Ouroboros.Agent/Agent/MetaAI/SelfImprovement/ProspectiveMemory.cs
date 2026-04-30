// ==========================================================
// Prospective Memory
// Einstein & McDaniel — time-based and event-based
// future-oriented remembering
// ==========================================================

namespace Ouroboros.Agent.MetaAI.SelfImprovement;

/// <summary>
/// Implements Einstein and McDaniel's model of prospective memory —
/// the ability to remember to perform intended actions at future points.
/// Supports both time-based and event-based reminders with a maximum
/// of 100 active reminders.
/// </summary>
public sealed class ProspectiveMemory : IProspectiveMemory
{
    private const int MaxActiveReminders = 100;
    private readonly ConcurrentDictionary<string, ProspectiveReminder> _reminders = new();
    private readonly ConcurrentDictionary<string, bool> _completed = new();
    private readonly ConcurrentDictionary<string, bool> _cancelled = new();

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
            TriggerCondition: string.Empty,
            Action: action,
            CreatedAt: DateTime.UtcNow,
            TriggerTime: triggerTime,
            IsTriggered: false);

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
            IsTriggered: false);

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
            if (r.IsTriggered || IsCompleted(r.Id) || IsCancelled(r.Id))
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
        if (_reminders.TryGetValue(id, out var r) && !IsCancelled(id))
        {
            _completed[id] = true;
            var updated = r with { IsTriggered = true };
            _reminders.TryUpdate(id, updated, r);
            return true;
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
        if (_reminders.ContainsKey(id) && !IsCompleted(id))
        {
            _cancelled[id] = true;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Returns all pending (not triggered, not completed, not cancelled) reminders.
    /// </summary>
    public IReadOnlyList<ProspectiveReminder> GetPendingReminders()
    {
        return _reminders.Values
            .Where(r => !r.IsTriggered && !IsCompleted(r.Id) && !IsCancelled(r.Id))
            .OrderBy(r => r.TriggerTime ?? DateTime.MaxValue)
            .ToList();
    }

    /// <summary>
    /// Returns the total number of reminders (including completed and cancelled).
    /// </summary>
    public int TotalReminders => _reminders.Count;

    private bool IsCompleted(string id) => _completed.ContainsKey(id);

    private bool IsCancelled(string id) => _cancelled.ContainsKey(id);

    private void EnforceCapacity()
    {
        int activeCount = _reminders.Values.Count(r => !IsCompleted(r.Id) && !IsCancelled(r.Id));
        if (activeCount >= MaxActiveReminders)
        {
            // Remove oldest completed/cancelled reminders first
            var toRemove = _reminders
                .Where(kvp => IsCompleted(kvp.Key) || IsCancelled(kvp.Key))
                .OrderBy(kvp => kvp.Value.CreatedAt)
                .Take(10)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (string key in toRemove)
            {
                _reminders.TryRemove(key, out _);
                _completed.TryRemove(key, out _);
                _cancelled.TryRemove(key, out _);
            }

            // If still over capacity, reject
            activeCount = _reminders.Values.Count(r => !IsCompleted(r.Id) && !IsCancelled(r.Id));
            if (activeCount >= MaxActiveReminders)
                throw new InvalidOperationException(
                    $"Maximum active reminders ({MaxActiveReminders}) reached. Cancel or complete existing reminders first.");
        }
    }
}
