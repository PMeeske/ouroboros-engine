using Ouroboros.Pipeline.Verification;

namespace Ouroboros.Pipeline.Planning;

/// <summary>
/// Represents a plan candidate with associated symbolic properties.
/// </summary>
/// <param name="Plan">The plan being considered.</param>
/// <param name="Score">Symbolic score or ranking (higher is better).</param>
/// <param name="Explanation">Symbolic explanation for why this plan was selected.</param>
public sealed record PlanCandidate(Plan Plan, double Score, string Explanation);