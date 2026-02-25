namespace Ouroboros.Pipeline.WorldModel;

/// <summary>
/// Represents a constraint or rule that limits system behavior.
/// </summary>
/// <param name="Name">Unique identifier for the constraint.</param>
/// <param name="Rule">Description or expression of the constraint rule.</param>
/// <param name="Priority">Priority level (higher values = higher priority).</param>
public sealed record Constraint(
    string Name,
    string Rule,
    int Priority)
{
    /// <summary>
    /// Creates a constraint with default priority (0).
    /// </summary>
    /// <param name="name">Constraint name.</param>
    /// <param name="rule">Constraint rule.</param>
    /// <returns>A new constraint.</returns>
    public static Constraint Create(string name, string rule)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(rule);

        return new Constraint(name, rule, 0);
    }

    /// <summary>
    /// Creates a constraint with specified priority.
    /// </summary>
    /// <param name="name">Constraint name.</param>
    /// <param name="rule">Constraint rule.</param>
    /// <param name="priority">Priority level.</param>
    /// <returns>A new constraint.</returns>
    public static Constraint Create(string name, string rule, int priority)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(rule);

        return new Constraint(name, rule, priority);
    }

    /// <summary>
    /// Creates a high-priority constraint (priority 100).
    /// </summary>
    /// <param name="name">Constraint name.</param>
    /// <param name="rule">Constraint rule.</param>
    /// <returns>A new high-priority constraint.</returns>
    public static Constraint Critical(string name, string rule)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(rule);

        return new Constraint(name, rule, 100);
    }
}