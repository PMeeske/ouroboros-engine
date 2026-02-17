namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Factory class for creating common delegation strategy instances.
/// Provides convenient access to pre-configured strategies.
/// </summary>
public static class DelegationStrategyFactory
{
    /// <summary>
    /// Creates a capability-based delegation strategy.
    /// Selects agents based on their proficiency in required capabilities.
    /// </summary>
    /// <returns>A new <see cref="CapabilityBasedStrategy"/> instance.</returns>
    public static IDelegationStrategy ByCapability()
    {
        return new CapabilityBasedStrategy();
    }

    /// <summary>
    /// Creates a role-based delegation strategy.
    /// Selects agents based on role matching, with capability fallback.
    /// </summary>
    /// <returns>A new <see cref="RoleBasedStrategy"/> instance.</returns>
    public static IDelegationStrategy ByRole()
    {
        return new RoleBasedStrategy();
    }

    /// <summary>
    /// Creates a load-balancing delegation strategy.
    /// Selects the least busy available agent.
    /// </summary>
    /// <returns>A new <see cref="LoadBalancingStrategy"/> instance.</returns>
    public static IDelegationStrategy ByLoad()
    {
        return new LoadBalancingStrategy();
    }

    /// <summary>
    /// Creates a round-robin delegation strategy.
    /// Cycles through agents in order for fair distribution.
    /// </summary>
    /// <returns>A new <see cref="RoundRobinStrategy"/> instance.</returns>
    public static IDelegationStrategy RoundRobin()
    {
        return new RoundRobinStrategy();
    }

    /// <summary>
    /// Creates a best-fit delegation strategy.
    /// Uses weighted scoring across capability, availability, and success rate.
    /// </summary>
    /// <returns>A new <see cref="BestFitStrategy"/> instance.</returns>
    public static IDelegationStrategy BestFit()
    {
        return new BestFitStrategy();
    }

    /// <summary>
    /// Creates a composite delegation strategy from multiple weighted strategies.
    /// </summary>
    /// <param name="weighted">The strategies and their weights.</param>
    /// <returns>A new <see cref="CompositeStrategy"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="weighted"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when no strategies are provided.</exception>
    public static IDelegationStrategy Composite(params (IDelegationStrategy Strategy, double Weight)[] weighted)
    {
        return CompositeStrategy.Create(weighted);
    }

    /// <summary>
    /// Creates a balanced composite strategy combining capability, load, and best-fit approaches.
    /// </summary>
    /// <returns>A pre-configured <see cref="CompositeStrategy"/> with balanced weights.</returns>
    public static IDelegationStrategy Balanced()
    {
        return CompositeStrategy.Create(
            (new CapabilityBasedStrategy(), 0.35),
            (new LoadBalancingStrategy(), 0.35),
            (new BestFitStrategy(), 0.30));
    }
}