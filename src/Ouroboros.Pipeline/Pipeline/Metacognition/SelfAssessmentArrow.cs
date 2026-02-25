using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Provides Kleisli arrows for self-assessment operations in the pipeline.
/// Enables functional composition of self-assessment with other pipeline operations.
/// </summary>
public static class SelfAssessmentArrow
{
    /// <summary>
    /// Creates a Kleisli arrow that performs a complete self-assessment.
    /// </summary>
    /// <param name="assessor">The self-assessor to use.</param>
    /// <returns>A Kleisli arrow from Unit to SelfAssessmentResult.</returns>
    public static KleisliResult<Unit, SelfAssessmentResult, string> AssessArrow(ISelfAssessor assessor)
        => async _ => await assessor.AssessAsync();

    /// <summary>
    /// Creates a Kleisli arrow that assesses a specific dimension.
    /// </summary>
    /// <param name="assessor">The self-assessor to use.</param>
    /// <returns>A Kleisli arrow from PerformanceDimension to DimensionScore.</returns>
    public static KleisliResult<PerformanceDimension, DimensionScore, string> AssessDimensionArrow(
        ISelfAssessor assessor)
        => async dimension => await assessor.AssessDimensionAsync(dimension);

    /// <summary>
    /// Creates a Kleisli arrow that updates a capability belief with evidence.
    /// </summary>
    /// <param name="assessor">The self-assessor to use.</param>
    /// <returns>A Kleisli arrow from (capability, evidence) to CapabilityBelief.</returns>
    public static KleisliResult<(string Capability, double Evidence), CapabilityBelief, string> UpdateBeliefArrow(
        ISelfAssessor assessor)
        => input => Task.FromResult(assessor.UpdateBelief(input.Capability, input.Evidence));

    /// <summary>
    /// Creates a Kleisli arrow that queries a capability belief.
    /// </summary>
    /// <param name="assessor">The self-assessor to use.</param>
    /// <returns>A Kleisli arrow from capability name to optional CapabilityBelief.</returns>
    public static KleisliOption<string, CapabilityBelief> GetBeliefArrow(ISelfAssessor assessor)
        => capability => Task.FromResult(assessor.GetCapabilityBelief(capability));

    /// <summary>
    /// Creates a Kleisli arrow that calibrates confidence with sample data.
    /// </summary>
    /// <param name="assessor">The self-assessor to use.</param>
    /// <returns>A Kleisli arrow from calibration samples to Unit.</returns>
    public static KleisliResult<IEnumerable<(double Predicted, double Actual)>, Unit, string> CalibrateArrow(
        ISelfAssessor assessor)
        => samples => Task.FromResult(assessor.CalibrateConfidence(samples));

    /// <summary>
    /// Creates a composed arrow that assesses and then updates beliefs based on results.
    /// </summary>
    /// <param name="assessor">The self-assessor to use.</param>
    /// <param name="capabilityExtractor">Function to extract capability updates from assessment.</param>
    /// <returns>A composed Kleisli arrow.</returns>
    public static KleisliResult<Unit, SelfAssessmentResult, string> AssessAndUpdateArrow(
        ISelfAssessor assessor,
        Func<SelfAssessmentResult, IEnumerable<(string Capability, double Evidence)>> capabilityExtractor)
        => async _ =>
        {
            var assessmentResult = await assessor.AssessAsync();

            if (assessmentResult.IsFailure)
            {
                return assessmentResult;
            }

            var assessment = assessmentResult.Value;
            var updates = capabilityExtractor(assessment);

            foreach (var (capability, evidence) in updates)
            {
                var updateResult = assessor.UpdateBelief(capability, evidence);
                if (updateResult.IsFailure)
                {
                    // Log but continue - partial updates are acceptable
                }
            }

            return Result<SelfAssessmentResult, string>.Success(assessment);
        };

    /// <summary>
    /// Creates an arrow that performs assessment with automatic confidence calibration.
    /// </summary>
    /// <param name="assessor">The self-assessor to use.</param>
    /// <param name="historicalData">Historical prediction/actual pairs for calibration.</param>
    /// <returns>A Kleisli arrow that calibrates then assesses.</returns>
    public static KleisliResult<Unit, SelfAssessmentResult, string> CalibratedAssessArrow(
        ISelfAssessor assessor,
        IEnumerable<(double Predicted, double Actual)> historicalData)
        => async _ =>
        {
            // First calibrate
            var calibrationResult = assessor.CalibrateConfidence(historicalData);
            if (calibrationResult.IsFailure)
            {
                return Result<SelfAssessmentResult, string>.Failure(calibrationResult.Error);
            }

            // Then assess with calibrated confidence
            return await assessor.AssessAsync();
        };
}