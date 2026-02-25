namespace Ouroboros.Pipeline.WorldModel;

/// <summary>
/// Represents a capability that the system can perform.
/// </summary>
/// <param name="Name">Unique identifier for the capability.</param>
/// <param name="Description">Human-readable description of what the capability does.</param>
/// <param name="RequiredTools">List of tool names required to perform this capability.</param>
public sealed record Capability(
    string Name,
    string Description,
    ImmutableList<string> RequiredTools)
{
    /// <summary>
    /// Creates a capability with no required tools.
    /// </summary>
    /// <param name="name">Capability name.</param>
    /// <param name="description">Capability description.</param>
    /// <returns>A new capability.</returns>
    public static Capability Create(string name, string description)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);

        return new Capability(name, description, ImmutableList<string>.Empty);
    }

    /// <summary>
    /// Creates a capability with required tools.
    /// </summary>
    /// <param name="name">Capability name.</param>
    /// <param name="description">Capability description.</param>
    /// <param name="requiredTools">Tools required for this capability.</param>
    /// <returns>A new capability.</returns>
    public static Capability Create(string name, string description, params string[] requiredTools)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(requiredTools);

        return new Capability(name, description, requiredTools.ToImmutableList());
    }

    /// <summary>
    /// Checks if all required tools are available in the provided tool set.
    /// </summary>
    /// <param name="availableTools">Set of available tool names.</param>
    /// <returns>True if all required tools are available.</returns>
    public bool CanExecuteWith(IReadOnlySet<string> availableTools)
    {
        ArgumentNullException.ThrowIfNull(availableTools);

        return RequiredTools.All(availableTools.Contains);
    }
}