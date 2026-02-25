// <copyright file="CognitiveMonitor.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Metacognition;

using System.Collections.Immutable;
using Ouroboros.Core.Monads;

/// <summary>
/// Represents an event in cognitive processing.
/// Immutable record capturing the details of a cognitive event for monitoring and analysis.
/// </summary>
/// <param name="Id">Unique identifier for this cognitive event.</param>
/// <param name="EventType">The type of cognitive event.</param>
/// <param name="Description">Human-readable description of the event.</param>
/// <param name="Timestamp">When the event occurred.</param>
/// <param name="Severity">The severity level of the event.</param>
/// <param name="Context">Additional contextual information about the event.</param>
public sealed record CognitiveEvent(
    Guid Id,
    CognitiveEventType EventType,
    string Description,
    DateTime Timestamp,
    Severity Severity,
    ImmutableDictionary<string, object> Context)
{
    /// <summary>
    /// Creates a thought generation event.
    /// </summary>
    /// <param name="description">Description of the thought.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>A new CognitiveEvent for thought generation.</returns>
    public static CognitiveEvent Thought(string description, ImmutableDictionary<string, object>? context = null) => new(
        Id: Guid.NewGuid(),
        EventType: CognitiveEventType.ThoughtGenerated,
        Description: description,
        Timestamp: DateTime.UtcNow,
        Severity: Severity.Info,
        Context: context ?? ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Creates a decision event.
    /// </summary>
    /// <param name="description">Description of the decision.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>A new CognitiveEvent for a decision.</returns>
    public static CognitiveEvent Decision(string description, ImmutableDictionary<string, object>? context = null) => new(
        Id: Guid.NewGuid(),
        EventType: CognitiveEventType.DecisionMade,
        Description: description,
        Timestamp: DateTime.UtcNow,
        Severity: Severity.Info,
        Context: context ?? ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Creates an error detection event.
    /// </summary>
    /// <param name="description">Description of the error.</param>
    /// <param name="severity">The severity of the error.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>A new CognitiveEvent for an error.</returns>
    public static CognitiveEvent Error(string description, Severity severity = Severity.Warning, ImmutableDictionary<string, object>? context = null) => new(
        Id: Guid.NewGuid(),
        EventType: CognitiveEventType.ErrorDetected,
        Description: description,
        Timestamp: DateTime.UtcNow,
        Severity: severity,
        Context: context ?? ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Creates a confusion sensing event.
    /// </summary>
    /// <param name="description">Description of the confusion.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>A new CognitiveEvent for confusion.</returns>
    public static CognitiveEvent Confusion(string description, ImmutableDictionary<string, object>? context = null) => new(
        Id: Guid.NewGuid(),
        EventType: CognitiveEventType.ConfusionSensed,
        Description: description,
        Timestamp: DateTime.UtcNow,
        Severity: Severity.Warning,
        Context: context ?? ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Creates an insight gaining event.
    /// </summary>
    /// <param name="description">Description of the insight.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>A new CognitiveEvent for an insight.</returns>
    public static CognitiveEvent Insight(string description, ImmutableDictionary<string, object>? context = null) => new(
        Id: Guid.NewGuid(),
        EventType: CognitiveEventType.InsightGained,
        Description: description,
        Timestamp: DateTime.UtcNow,
        Severity: Severity.Info,
        Context: context ?? ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Creates an attention shift event.
    /// </summary>
    /// <param name="description">Description of the attention shift.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>A new CognitiveEvent for attention shift.</returns>
    public static CognitiveEvent AttentionChange(string description, ImmutableDictionary<string, object>? context = null) => new(
        Id: Guid.NewGuid(),
        EventType: CognitiveEventType.AttentionShift,
        Description: description,
        Timestamp: DateTime.UtcNow,
        Severity: Severity.Info,
        Context: context ?? ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Creates a goal activation event.
    /// </summary>
    /// <param name="goalDescription">Description of the activated goal.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>A new CognitiveEvent for goal activation.</returns>
    public static CognitiveEvent GoalStart(string goalDescription, ImmutableDictionary<string, object>? context = null) => new(
        Id: Guid.NewGuid(),
        EventType: CognitiveEventType.GoalActivated,
        Description: goalDescription,
        Timestamp: DateTime.UtcNow,
        Severity: Severity.Info,
        Context: context ?? ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Creates a goal completion event.
    /// </summary>
    /// <param name="goalDescription">Description of the completed goal.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>A new CognitiveEvent for goal completion.</returns>
    public static CognitiveEvent GoalEnd(string goalDescription, ImmutableDictionary<string, object>? context = null) => new(
        Id: Guid.NewGuid(),
        EventType: CognitiveEventType.GoalCompleted,
        Description: goalDescription,
        Timestamp: DateTime.UtcNow,
        Severity: Severity.Info,
        Context: context ?? ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Creates an uncertainty detection event.
    /// </summary>
    /// <param name="description">Description of the uncertainty.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>A new CognitiveEvent for uncertainty.</returns>
    public static CognitiveEvent UncertaintyDetected(string description, ImmutableDictionary<string, object>? context = null) => new(
        Id: Guid.NewGuid(),
        EventType: CognitiveEventType.Uncertainty,
        Description: description,
        Timestamp: DateTime.UtcNow,
        Severity: Severity.Warning,
        Context: context ?? ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Creates a contradiction detection event.
    /// </summary>
    /// <param name="description">Description of the contradiction.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>A new CognitiveEvent for contradiction.</returns>
    public static CognitiveEvent ContradictionDetected(string description, ImmutableDictionary<string, object>? context = null) => new(
        Id: Guid.NewGuid(),
        EventType: CognitiveEventType.Contradiction,
        Description: description,
        Timestamp: DateTime.UtcNow,
        Severity: Severity.Critical,
        Context: context ?? ImmutableDictionary<string, object>.Empty);

    /// <summary>
    /// Creates a copy of this event with additional context.
    /// </summary>
    /// <param name="key">The context key.</param>
    /// <param name="value">The context value.</param>
    /// <returns>A new CognitiveEvent with the added context.</returns>
    public CognitiveEvent WithContext(string key, object value)
        => this with { Context = Context.SetItem(key, value) };

    /// <summary>
    /// Creates a copy of this event with merged context.
    /// </summary>
    /// <param name="additionalContext">Additional context to merge.</param>
    /// <returns>A new CognitiveEvent with merged context.</returns>
    public CognitiveEvent WithMergedContext(ImmutableDictionary<string, object> additionalContext)
        => this with { Context = Context.SetItems(additionalContext) };
}