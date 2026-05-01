// <copyright file="DistinctionTracker.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.LawsOfForm;

/// <summary>
/// Per-reasoning-context distinction state holder. Each reasoning context
/// (typically a query, agent turn, or atom-space session) gets its own
/// tracker — distinctions made in one context do not leak into another.
/// </summary>
public sealed class DistinctionTracker
{
    private readonly Dictionary<string, DistinctionState> _states = new();

    /// <summary>
    /// Reads the current state of a named distinction. Unknown names
    /// resolve to <see cref="DistinctionState.Void"/>.
    /// </summary>
    /// <param name="name">The distinction name.</param>
    /// <returns>The recorded state or <see cref="DistinctionState.Void"/>.</returns>
    public DistinctionState Get(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _states.TryGetValue(name, out DistinctionState state) ? state : DistinctionState.Void;
    }

    /// <summary>
    /// Sets a distinction state explicitly.
    /// </summary>
    /// <param name="name">The distinction name.</param>
    /// <param name="state">The new state.</param>
    public void Set(string name, DistinctionState state)
    {
        ArgumentNullException.ThrowIfNull(name);
        _states[name] = state;
    }

    /// <summary>
    /// Toggles a distinction Void <-> Mark, leaving Imaginary unchanged.
    /// This is the semantic of <see cref="CrossOperation"/>.
    /// </summary>
    /// <param name="name">The distinction name.</param>
    /// <returns>The state after toggling.</returns>
    public DistinctionState Toggle(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        DistinctionState current = Get(name);
        DistinctionState next = current switch
        {
            DistinctionState.Mark => DistinctionState.Void,
            DistinctionState.Void => DistinctionState.Mark,
            _ => current,
        };
        _states[name] = next;
        return next;
    }

    /// <summary>
    /// Gets the count of named distinctions currently tracked.
    /// </summary>
    public int Count => _states.Count;

    /// <summary>
    /// Clears all tracked distinctions.
    /// </summary>
    public void Reset() => _states.Clear();
}
