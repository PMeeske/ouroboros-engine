namespace Ouroboros.Providers;

/// <summary>
/// Election strategy algorithms for selecting the best response from candidates.
/// </summary>
public enum ElectionStrategy
{
    /// <summary>Simple majority: highest score wins.</summary>
    Majority,
    /// <summary>Weighted by source reliability.</summary>
    WeightedMajority,
    /// <summary>Borda count: rank-based scoring.</summary>
    BordaCount,
    /// <summary>Condorcet: pairwise comparison winner.</summary>
    Condorcet,
    /// <summary>Instant runoff: eliminate lowest, redistribute.</summary>
    InstantRunoff,
    /// <summary>Approval voting: count approvals above threshold.</summary>
    ApprovalVoting,
    /// <summary>Master model decides winner.</summary>
    MasterDecision
}