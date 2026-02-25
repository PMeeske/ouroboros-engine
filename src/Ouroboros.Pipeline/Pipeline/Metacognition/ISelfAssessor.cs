using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Interface for performing self-assessment of system capabilities and performance.
/// Implements introspective evaluation with Bayesian belief updates.
/// </summary>
public interface ISelfAssessor
{
    /// <summary>
    /// Performs a comprehensive self-assessment across all dimensions.
    /// </summary>
    /// <returns>A complete self-assessment result.</returns>
    Task<Result<SelfAssessmentResult, string>> AssessAsync();

    /// <summary>
    /// Assesses a single performance dimension.
    /// </summary>
    /// <param name="dimension">The dimension to assess.</param>
    /// <returns>The score for the specified dimension.</returns>
    Task<Result<DimensionScore, string>> AssessDimensionAsync(PerformanceDimension dimension);

    /// <summary>
    /// Queries the current belief about a specific capability.
    /// </summary>
    /// <param name="capability">The capability name to query.</param>
    /// <returns>The current capability belief if known.</returns>
    Option<CapabilityBelief> GetCapabilityBelief(string capability);

    /// <summary>
    /// Updates a capability belief with new evidence using Bayesian inference.
    /// </summary>
    /// <param name="capability">The capability to update.</param>
    /// <param name="evidence">The observed evidence (success rate 0-1).</param>
    /// <returns>The updated capability belief.</returns>
    Result<CapabilityBelief, string> UpdateBelief(string capability, double evidence);

    /// <summary>
    /// Returns all current capability beliefs.
    /// </summary>
    /// <returns>An immutable dictionary of all capability beliefs.</returns>
    ImmutableDictionary<string, CapabilityBelief> GetAllBeliefs();

    /// <summary>
    /// Calibrates confidence estimates using historical accuracy data.
    /// Adjusts confidence calibration based on predicted vs actual outcomes.
    /// </summary>
    /// <param name="samples">Pairs of (predicted confidence, actual success rate).</param>
    /// <returns>Unit on success, error message on failure.</returns>
    Result<Unit, string> CalibrateConfidence(IEnumerable<(double Predicted, double Actual)> samples);
}