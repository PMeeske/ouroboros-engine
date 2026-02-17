using System.Runtime.CompilerServices;
using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Bayesian self-assessor implementing ISelfAssessor with probabilistic belief updates.
/// Maintains and updates beliefs about capabilities using Bayesian inference.
/// </summary>
public sealed class BayesianSelfAssessor : ISelfAssessor
{
    private readonly ConcurrentDictionary<string, CapabilityBelief> capabilityBeliefs;
    private readonly ConcurrentDictionary<PerformanceDimension, DimensionScore> dimensionScores;
    private double confidenceCalibrationFactor;

    /// <summary>
    /// Initializes a new instance of the <see cref="BayesianSelfAssessor"/> class.
    /// </summary>
    public BayesianSelfAssessor()
    {
        this.capabilityBeliefs = new ConcurrentDictionary<string, CapabilityBelief>();
        this.dimensionScores = new ConcurrentDictionary<PerformanceDimension, DimensionScore>();
        this.confidenceCalibrationFactor = 1.0;

        // Initialize dimension scores with uninformative priors
        foreach (var dimension in Enum.GetValues<PerformanceDimension>())
        {
            this.dimensionScores[dimension] = DimensionScore.Unknown(dimension);
        }
    }

    /// <summary>
    /// Initializes a new instance with pre-existing beliefs.
    /// </summary>
    /// <param name="initialBeliefs">Initial capability beliefs.</param>
    /// <param name="initialScores">Initial dimension scores.</param>
    public BayesianSelfAssessor(
        IEnumerable<CapabilityBelief> initialBeliefs,
        IEnumerable<DimensionScore> initialScores)
        : this()
    {
        foreach (var belief in initialBeliefs)
        {
            this.capabilityBeliefs[belief.CapabilityName] = belief;
        }

        foreach (var score in initialScores)
        {
            this.dimensionScores[score.Dimension] = score;
        }
    }

    /// <inheritdoc/>
    public async Task<Result<SelfAssessmentResult, string>> AssessAsync()
    {
        try
        {
            var tasks = Enum.GetValues<PerformanceDimension>()
                .Select(d => AssessDimensionAsync(d))
                .ToList();

            var results = await Task.WhenAll(tasks);

            var scores = ImmutableDictionary.CreateBuilder<PerformanceDimension, DimensionScore>();
            foreach (var result in results)
            {
                if (result.IsSuccess)
                {
                    scores[result.Value.Dimension] = result.Value;
                }
            }

            var assessment = SelfAssessmentResult.FromDimensionScores(scores.ToImmutable());
            return Result<SelfAssessmentResult, string>.Success(assessment);
        }
        catch (Exception ex)
        {
            return Result<SelfAssessmentResult, string>.Failure($"Assessment failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Task<Result<DimensionScore, string>> AssessDimensionAsync(PerformanceDimension dimension)
    {
        try
        {
            var currentScore = this.dimensionScores.GetOrAdd(
                dimension,
                d => DimensionScore.Unknown(d));

            // Apply confidence calibration
            var calibratedScore = currentScore with
            {
                Confidence = Math.Min(1.0, currentScore.Confidence * this.confidenceCalibrationFactor),
            };

            return Task.FromResult(Result<DimensionScore, string>.Success(calibratedScore));
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                Result<DimensionScore, string>.Failure($"Dimension assessment failed: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Option<CapabilityBelief> GetCapabilityBelief(string capability)
    {
        if (string.IsNullOrWhiteSpace(capability))
        {
            return Option<CapabilityBelief>.None();
        }

        return this.capabilityBeliefs.TryGetValue(capability, out var belief)
            ? Option<CapabilityBelief>.Some(belief)
            : Option<CapabilityBelief>.None();
    }

    /// <inheritdoc/>
    public Result<CapabilityBelief, string> UpdateBelief(string capability, double evidence)
    {
        if (string.IsNullOrWhiteSpace(capability))
        {
            return Result<CapabilityBelief, string>.Failure("Capability name cannot be empty.");
        }

        if (evidence < 0.0 || evidence > 1.0)
        {
            return Result<CapabilityBelief, string>.Failure($"Evidence must be in [0, 1], got {evidence}.");
        }

        try
        {
            var updatedBelief = this.capabilityBeliefs.AddOrUpdate(
                capability,
                _ => CapabilityBelief.Uninformative(capability).WithBayesianUpdate(evidence),
                (_, existing) => existing.WithBayesianUpdate(evidence));

            return Result<CapabilityBelief, string>.Success(updatedBelief);
        }
        catch (Exception ex)
        {
            return Result<CapabilityBelief, string>.Failure($"Belief update failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public ImmutableDictionary<string, CapabilityBelief> GetAllBeliefs()
        => this.capabilityBeliefs.ToImmutableDictionary();

    /// <inheritdoc/>
    public Result<Unit, string> CalibrateConfidence(IEnumerable<(double Predicted, double Actual)> samples)
    {
        try
        {
            var sampleList = samples.ToList();
            if (sampleList.Count == 0)
            {
                return Result<Unit, string>.Success(Unit.Value);
            }

            // Compute calibration error (Expected Calibration Error approach)
            // If predictions are overconfident, reduce calibration factor
            // If underconfident, increase it

            var totalError = 0.0;
            var totalBias = 0.0;

            foreach (var (predicted, actual) in sampleList)
            {
                var clampedPredicted = Math.Clamp(predicted, 0.0, 1.0);
                var clampedActual = Math.Clamp(actual, 0.0, 1.0);

                totalError += Math.Abs(clampedPredicted - clampedActual);
                totalBias += clampedPredicted - clampedActual; // Positive = overconfident
            }

            var meanBias = totalBias / sampleList.Count;

            // Adjust calibration factor based on bias
            // Overconfident → reduce factor, Underconfident → increase factor
            var adjustment = 1.0 - (meanBias * 0.5); // Gradual adjustment
            this.confidenceCalibrationFactor = Math.Clamp(
                this.confidenceCalibrationFactor * adjustment,
                0.1,
                2.0);

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Calibration failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates a dimension score with new evidence.
    /// </summary>
    /// <param name="dimension">The dimension to update.</param>
    /// <param name="observedScore">The observed score value.</param>
    /// <param name="weight">The weight of the observation.</param>
    /// <param name="evidence">Evidence description.</param>
    /// <returns>The updated dimension score.</returns>
    public Result<DimensionScore, string> UpdateDimensionScore(
        PerformanceDimension dimension,
        double observedScore,
        double weight,
        string evidence)
    {
        try
        {
            var updatedScore = this.dimensionScores.AddOrUpdate(
                dimension,
                d => DimensionScore.Unknown(d).WithBayesianUpdate(observedScore, weight, evidence),
                (_, existing) => existing.WithBayesianUpdate(observedScore, weight, evidence));

            return Result<DimensionScore, string>.Success(updatedScore);
        }
        catch (Exception ex)
        {
            return Result<DimensionScore, string>.Failure($"Dimension score update failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the current confidence calibration factor.
    /// </summary>
    /// <returns>The calibration factor applied to confidence values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetCalibrationFactor() => this.confidenceCalibrationFactor;
}