// ==========================================================
// Global Workspace Theory — Entropy-Based Intrinsic Drive
// Plan 6: IntrinsicDrive
// ==========================================================

namespace Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

/// <summary>
/// Describes the current intrinsic drive state based on workspace entropy.
/// </summary>
public enum DriveState
{
    /// <summary>Low entropy — system is bored, should explore</summary>
    Bored,

    /// <summary>Healthy entropy — normal operation</summary>
    Healthy,

    /// <summary>High entropy — system is overwhelmed, should focus</summary>
    Overwhelmed
}

/// <summary>
/// Selects actions based on workspace entropy without external reward.
/// </summary>
public sealed class IntrinsicDrive
{
    /// <summary>
    /// Lower entropy threshold (0.0–0.3 = bored).
    /// </summary>
    public double BoredThreshold { get; init; } = 0.3;

    /// <summary>
    /// Upper entropy threshold (0.7–1.0 = overwhelmed).
    /// </summary>
    public double OverwhelmedThreshold { get; init; } = 0.7;

    /// <summary>
    /// Salience bonus applied to exploration candidates when bored.
    /// </summary>
    public double ExplorationBonus { get; init; } = 0.15;

    /// <summary>
    /// Salience bonus applied to focus candidates when overwhelmed.
    /// </summary>
    public double FocusBonus { get; init; } = 0.10;

    /// <summary>
    /// Penalty applied to novelty when overwhelmed.
    /// </summary>
    public double NoveltySuppression { get; init; } = 0.20;

    /// <summary>
    /// Determines the current drive state from entropy.
    /// </summary>
    /// <param name="entropy">Normalized entropy in range [0, 1]</param>
    /// <returns>Current drive state</returns>
    public DriveState EvaluateState(double entropy)
    {
        if (entropy < BoredThreshold)
        {
            return DriveState.Bored;
        }

        if (entropy > OverwhelmedThreshold)
        {
            return DriveState.Overwhelmed;
        }

        return DriveState.Healthy;
    }

    /// <summary>
    /// Adjusts a candidate's salience based on the current drive state.
    /// </summary>
    /// <param name="candidate">The candidate to adjust</param>
    /// <param name="baseSalience">The base salience score</param>
    /// <param name="state">Current drive state</param>
    /// <returns>Adjusted salience</returns>
    public double AdjustSalience(Candidate candidate, double baseSalience, DriveState state)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return state switch
        {
            DriveState.Bored => Math.Clamp(baseSalience + ExplorationBonus, 0.0, 1.0),
            DriveState.Overwhelmed => Math.Clamp(baseSalience + FocusBonus - (candidate.Novelty * NoveltySuppression), 0.0, 1.0),
            _ => baseSalience
        };
    }
}
