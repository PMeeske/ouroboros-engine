// <copyright file="AgentIdentity.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Represents the unique identity and profile of an agent in a multi-agent system.
/// </summary>
/// <param name="Id">The unique identifier for this agent.</param>
/// <param name="Name">The human-readable name of this agent.</param>
/// <param name="Role">The high-level role classification of this agent.</param>
/// <param name="Capabilities">The list of capabilities this agent possesses.</param>
/// <param name="Metadata">Additional metadata associated with this agent.</param>
/// <param name="CreatedAt">The timestamp when this agent identity was created.</param>
public sealed record AgentIdentity(
    Guid Id,
    string Name,
    AgentRole Role,
    ImmutableList<AgentCapability> Capabilities,
    ImmutableDictionary<string, object> Metadata,
    DateTime CreatedAt)
{
    /// <summary>
    /// Creates a new agent identity with the specified name and role.
    /// </summary>
    /// <param name="name">The human-readable name of the agent.</param>
    /// <param name="role">The high-level role classification of the agent.</param>
    /// <returns>A new <see cref="AgentIdentity"/> instance with a generated ID and current timestamp.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public static AgentIdentity Create(string name, AgentRole role)
    {
        ArgumentNullException.ThrowIfNull(name);

        return new AgentIdentity(
            Guid.NewGuid(),
            name,
            role,
            ImmutableList<AgentCapability>.Empty,
            ImmutableDictionary<string, object>.Empty,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a new agent identity with the specified capability added.
    /// </summary>
    /// <param name="capability">The capability to add to this agent.</param>
    /// <returns>A new <see cref="AgentIdentity"/> instance with the capability added.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="capability"/> is null.</exception>
    public AgentIdentity WithCapability(AgentCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        return this with { Capabilities = Capabilities.Add(capability) };
    }

    /// <summary>
    /// Creates a new agent identity with the specified metadata entry added or updated.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>A new <see cref="AgentIdentity"/> instance with the metadata entry.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> or <paramref name="value"/> is null.</exception>
    public AgentIdentity WithMetadata(string key, object value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        return this with { Metadata = Metadata.SetItem(key, value) };
    }

    /// <summary>
    /// Gets a capability by name if it exists.
    /// </summary>
    /// <param name="name">The name of the capability to retrieve.</param>
    /// <returns>An <see cref="Option{T}"/> containing the capability if found, or None otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public Option<AgentCapability> GetCapability(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        AgentCapability? capability = Capabilities.Find(c => c.Name.Equals(name, StringComparison.Ordinal));
        return capability is not null
            ? Option<AgentCapability>.Some(capability)
            : Option<AgentCapability>.None();
    }

    /// <summary>
    /// Determines whether this agent has the specified capability.
    /// </summary>
    /// <param name="name">The name of the capability to check for.</param>
    /// <returns><c>true</c> if the agent has the capability; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public bool HasCapability(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return Capabilities.Exists(c => c.Name.Equals(name, StringComparison.Ordinal));
    }

    /// <summary>
    /// Gets the proficiency level for a specific capability.
    /// </summary>
    /// <param name="capabilityName">The name of the capability.</param>
    /// <returns>The proficiency level (0.0 to 1.0) if the capability exists, or 0.0 if not found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="capabilityName"/> is null.</exception>
    public double GetProficiencyFor(string capabilityName)
    {
        ArgumentNullException.ThrowIfNull(capabilityName);

        AgentCapability? capability = Capabilities.Find(c => c.Name.Equals(capabilityName, StringComparison.Ordinal));
        return capability?.Proficiency ?? 0.0;
    }

    /// <summary>
    /// Gets all capabilities with proficiency above the specified minimum threshold.
    /// </summary>
    /// <param name="minProficiency">The minimum proficiency threshold (exclusive).</param>
    /// <returns>A read-only list of capabilities above the threshold.</returns>
    public IReadOnlyList<AgentCapability> GetCapabilitiesAbove(double minProficiency)
    {
        return Capabilities.FindAll(c => c.Proficiency > minProficiency);
    }
}