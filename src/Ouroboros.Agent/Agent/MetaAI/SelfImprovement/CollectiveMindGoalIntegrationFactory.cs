namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Factory methods for creating CollectiveMindGoalIntegration instances.
/// </summary>
public static class CollectiveMindGoalIntegrationFactory
{
    /// <summary>
    /// Creates an integration that uses both CollectiveMind and GoalHierarchy.
    /// </summary>
    public static CollectiveMindGoalIntegration CreateWithHierarchy(
        CollectiveMind mind,
        IGoalHierarchy goalHierarchy) =>
        new(mind, goalHierarchy);

    /// <summary>
    /// Creates a standalone integration using only CollectiveMind.
    /// </summary>
    public static CollectiveMindGoalIntegration CreateStandalone(CollectiveMind mind) =>
        new(mind);

    /// <summary>
    /// Creates an integration with a pre-configured decomposed CollectiveMind.
    /// </summary>
    public static CollectiveMindGoalIntegration CreateDecomposed(
        ChatRuntimeSettings? settings = null)
    {
        var mind = CollectiveMindFactory.CreateDecomposed(settings);
        return new CollectiveMindGoalIntegration(mind);
    }

    /// <summary>
    /// Creates an integration optimized for local-first execution.
    /// </summary>
    public static CollectiveMindGoalIntegration CreateLocalFirst(
        string localModel = "llama3.2",
        ChatRuntimeSettings? settings = null)
    {
        var mind = CollectiveMindFactory.CreateLocalFirstDecomposed(localModel, settings: settings);
        return new CollectiveMindGoalIntegration(mind);
    }
}