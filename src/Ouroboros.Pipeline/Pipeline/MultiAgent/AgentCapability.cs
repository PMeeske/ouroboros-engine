namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Represents a capability that an agent possesses, including proficiency level.
/// </summary>
/// <param name="Name">The unique name identifying this capability.</param>
/// <param name="Description">A human-readable description of what this capability enables.</param>
/// <param name="Proficiency">The agent's proficiency level for this capability, ranging from 0.0 to 1.0.</param>
/// <param name="RequiredTools">The list of tools required to exercise this capability.</param>
public sealed record AgentCapability(
    string Name,
    string Description,
    double Proficiency,
    ImmutableList<string> RequiredTools)
{
    /// <summary>
    /// Creates a new agent capability with the specified parameters.
    /// </summary>
    /// <param name="name">The unique name identifying this capability.</param>
    /// <param name="description">A human-readable description of what this capability enables.</param>
    /// <param name="proficiency">The agent's proficiency level, ranging from 0.0 to 1.0. Defaults to 1.0.</param>
    /// <param name="tools">The tools required to exercise this capability.</param>
    /// <returns>A new <see cref="AgentCapability"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="description"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="proficiency"/> is not between 0.0 and 1.0.</exception>
    public static AgentCapability Create(string name, string description, double proficiency = 1.0, params string[] tools)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(tools);

        if (proficiency < 0.0 || proficiency > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(proficiency), proficiency, "Proficiency must be between 0.0 and 1.0.");
        }

        return new AgentCapability(
            name,
            description,
            proficiency,
            tools.ToImmutableList());
    }
}