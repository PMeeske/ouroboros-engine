namespace Ouroboros.Pipeline.Council;

/// <summary>
/// Enumeration of debate phases in the council protocol.
/// </summary>
public enum DebatePhase
{
    /// <summary>Each agent presents their initial position.</summary>
    Proposal,

    /// <summary>Agents critique positions and present counterarguments.</summary>
    Challenge,

    /// <summary>Agents revise positions based on feedback.</summary>
    Refinement,

    /// <summary>Weighted voting mechanism.</summary>
    Voting,

    /// <summary>Orchestrator synthesizes consensus or flags conflicts.</summary>
    Synthesis
}