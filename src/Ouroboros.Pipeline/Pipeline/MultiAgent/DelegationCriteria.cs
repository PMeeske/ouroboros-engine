// <copyright file="DelegationStrategy.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Represents the criteria used to delegate a task to an agent.
/// Encapsulates goal requirements, capability constraints, and agent preferences.
/// </summary>
/// <param name="Goal">The goal that needs to be delegated.</param>
/// <param name="RequiredCapabilities">The list of capabilities required to complete the goal.</param>
/// <param name="MinProficiency">The minimum proficiency level required (0.0 to 1.0).</param>
/// <param name="PreferAvailable">Whether to prefer agents that are currently available.</param>
/// <param name="PreferredRole">The preferred role for the agent, if any.</param>
public sealed record DelegationCriteria(
    Goal Goal,
    IReadOnlyList<string> RequiredCapabilities,
    double MinProficiency,
    bool PreferAvailable,
    AgentRole? PreferredRole)
{
    /// <summary>
    /// Creates delegation criteria from a goal with default settings.
    /// </summary>
    /// <param name="goal">The goal to create criteria for.</param>
    /// <returns>A new <see cref="DelegationCriteria"/> with default settings.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="goal"/> is null.</exception>
    public static DelegationCriteria FromGoal(Goal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);

        return new DelegationCriteria(
            Goal: goal,
            RequiredCapabilities: Array.Empty<string>(),
            MinProficiency: 0.0,
            PreferAvailable: true,
            PreferredRole: null);
    }

    /// <summary>
    /// Creates a new criteria with the specified minimum proficiency level.
    /// </summary>
    /// <param name="minProficiency">The minimum proficiency level (0.0 to 1.0).</param>
    /// <returns>A new <see cref="DelegationCriteria"/> with the updated proficiency requirement.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="minProficiency"/> is not between 0.0 and 1.0.
    /// </exception>
    public DelegationCriteria WithMinProficiency(double minProficiency)
    {
        if (minProficiency < 0.0 || minProficiency > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minProficiency),
                minProficiency,
                "Minimum proficiency must be between 0.0 and 1.0.");
        }

        return this with { MinProficiency = minProficiency };
    }

    /// <summary>
    /// Creates a new criteria with the specified preferred role.
    /// </summary>
    /// <param name="role">The preferred role for the agent.</param>
    /// <returns>A new <see cref="DelegationCriteria"/> with the preferred role set.</returns>
    public DelegationCriteria WithPreferredRole(AgentRole role)
    {
        return this with { PreferredRole = role };
    }

    /// <summary>
    /// Creates a new criteria with an additional required capability.
    /// </summary>
    /// <param name="capability">The capability to require.</param>
    /// <returns>A new <see cref="DelegationCriteria"/> with the capability added to requirements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="capability"/> is null.</exception>
    public DelegationCriteria RequireCapability(string capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        List<string> newCapabilities = new(RequiredCapabilities) { capability };
        return this with { RequiredCapabilities = newCapabilities };
    }

    /// <summary>
    /// Creates a new criteria with the availability preference set.
    /// </summary>
    /// <param name="preferAvailable">Whether to prefer available agents.</param>
    /// <returns>A new <see cref="DelegationCriteria"/> with the updated preference.</returns>
    public DelegationCriteria WithAvailabilityPreference(bool preferAvailable)
    {
        return this with { PreferAvailable = preferAvailable };
    }
}