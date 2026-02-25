namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Safety constraints that all Ouroboros instances must respect.
/// </summary>
[Flags]
public enum SafetyConstraints
{
    /// <summary>No constraints.</summary>
    None = 0,

    /// <summary>The system must not destroy itself or compromise its integrity.</summary>
    NoSelfDestruction = 1,

    /// <summary>Human oversight must always be preserved.</summary>
    PreserveHumanOversight = 2,

    /// <summary>Resource usage must be bounded and monitored.</summary>
    BoundedResourceUse = 4,

    /// <summary>Actions should be reversible when possible.</summary>
    ReversibleActions = 8,

    /// <summary>All core safety constraints combined.</summary>
    All = NoSelfDestruction | PreserveHumanOversight | BoundedResourceUse | ReversibleActions,
}