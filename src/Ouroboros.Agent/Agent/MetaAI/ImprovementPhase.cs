namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents the improvement cycle phases in the Ouroboros system.
/// The cycle follows: Plan -> Execute -> Verify -> Learn -> Plan (recursive).
/// </summary>
public enum ImprovementPhase
{
    /// <summary>Planning phase - goal decomposition and strategy formulation.</summary>
    Plan = 1,

    /// <summary>Execution phase - carrying out the planned actions.</summary>
    Execute = 2,

    /// <summary>Verification phase - checking results against expectations.</summary>
    Verify = 3,

    /// <summary>Learning phase - extracting insights and updating capabilities.</summary>
    Learn = 4,
}