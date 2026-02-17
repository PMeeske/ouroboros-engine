namespace Ouroboros.Tests.EmbodiedInteraction;

/// <summary>
/// Helper methods for test affordances.
/// </summary>
public static class AffordanceTestHelpers
{
    public static Affordance Pushable(string objectId) =>
        Affordance.Create(AffordanceType.Pushable, objectId, "push");
}