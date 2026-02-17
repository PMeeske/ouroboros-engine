using Ouroboros.Abstractions;

namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Represents a belief about a specific capability.
/// Uses Bayesian inference to maintain and update capability estimates.
/// </summary>
/// <param name="CapabilityName">The name identifying this capability.</param>
/// <param name="Proficiency">Estimated proficiency level in [0, 1].</param>
/// <param name="Uncertainty">Uncertainty in the estimate in [0, 1] (epistemic uncertainty).</param>
/// <param name="LastValidated">When this belief was last validated against evidence.</param>
/// <param name="ValidationCount">Number of times this belief has been validated.</param>
public sealed record CapabilityBelief(
    string CapabilityName,
    double Proficiency,
    double Uncertainty,
    DateTime LastValidated,
    int ValidationCount)
{
    /// <summary>
    /// Creates a new capability belief with maximum uncertainty (uninformative prior).
    /// </summary>
    /// <param name="capabilityName">The capability name.</param>
    /// <returns>A new CapabilityBelief with 0.5 proficiency and high uncertainty.</returns>
    public static CapabilityBelief Uninformative(string capabilityName) => new(
        CapabilityName: capabilityName,
        Proficiency: 0.5,
        Uncertainty: 1.0,
        LastValidated: DateTime.MinValue,
        ValidationCount: 0);

    /// <summary>
    /// Creates a capability belief with initial estimates.
    /// </summary>
    /// <param name="capabilityName">The capability name.</param>
    /// <param name="proficiency">Initial proficiency estimate.</param>
    /// <param name="uncertainty">Initial uncertainty.</param>
    /// <returns>A new CapabilityBelief.</returns>
    public static CapabilityBelief Create(
        string capabilityName,
        double proficiency,
        double uncertainty) => new(
        CapabilityName: capabilityName,
        Proficiency: Math.Clamp(proficiency, 0.0, 1.0),
        Uncertainty: Math.Clamp(uncertainty, 0.0, 1.0),
        LastValidated: DateTime.UtcNow,
        ValidationCount: 1);

    /// <summary>
    /// Updates the belief with new evidence using Bayesian inference.
    /// Uses a beta distribution model for proficiency estimation.
    /// </summary>
    /// <param name="observedSuccess">The observed success rate (0-1) from evidence.</param>
    /// <param name="sampleSize">Number of observations in the evidence.</param>
    /// <returns>Updated CapabilityBelief with posterior estimates.</returns>
    public CapabilityBelief WithBayesianUpdate(double observedSuccess, int sampleSize = 1)
    {
        // Beta distribution update: prior α, β mapped from proficiency and uncertainty
        // Prior pseudo-counts based on current belief strength
        var beliefStrength = Math.Max(1, ValidationCount);
        var priorAlpha = Proficiency * beliefStrength;
        var priorBeta = (1.0 - Proficiency) * beliefStrength;

        // Likelihood: binomial model
        var likelihoodAlpha = observedSuccess * sampleSize;
        var likelihoodBeta = (1.0 - observedSuccess) * sampleSize;

        // Posterior
        var posteriorAlpha = priorAlpha + likelihoodAlpha;
        var posteriorBeta = priorBeta + likelihoodBeta;
        var totalPseudoCount = posteriorAlpha + posteriorBeta;

        // Posterior mean
        var posteriorProficiency = posteriorAlpha / totalPseudoCount;

        // Posterior variance → uncertainty (scaled)
        var posteriorVariance = (posteriorAlpha * posteriorBeta) /
                                (totalPseudoCount * totalPseudoCount * (totalPseudoCount + 1));
        var posteriorUncertainty = Math.Sqrt(posteriorVariance) * 3.46; // Scale to [0,1] range approximately

        return this with
        {
            Proficiency = Math.Clamp(posteriorProficiency, 0.0, 1.0),
            Uncertainty = Math.Clamp(posteriorUncertainty, 0.0, 1.0),
            LastValidated = DateTime.UtcNow,
            ValidationCount = ValidationCount + sampleSize,
        };
    }

    /// <summary>
    /// Computes the expected value with uncertainty bounds.
    /// Returns the proficiency with a credible interval.
    /// </summary>
    /// <param name="credibleLevel">The credible interval level (e.g., 0.95 for 95%).</param>
    /// <returns>Tuple of (lower bound, expected, upper bound).</returns>
    public (double Lower, double Expected, double Upper) GetCredibleInterval(double credibleLevel = 0.95)
    {
        // Approximate credible interval using normal approximation
        var z = credibleLevel switch
        {
            >= 0.99 => 2.576,
            >= 0.95 => 1.96,
            >= 0.90 => 1.645,
            _ => 1.0,
        };

        var margin = z * Uncertainty * 0.5;
        return (
            Lower: Math.Max(0.0, Proficiency - margin),
            Expected: Proficiency,
            Upper: Math.Min(1.0, Proficiency + margin));
    }

    /// <summary>
    /// Validates the capability belief values.
    /// </summary>
    /// <returns>A Result indicating validity or validation error.</returns>
    public Result<Unit, string> Validate()
    {
        if (string.IsNullOrWhiteSpace(CapabilityName))
        {
            return Result<Unit, string>.Failure("Capability name cannot be empty.");
        }

        if (Proficiency < 0.0 || Proficiency > 1.0)
        {
            return Result<Unit, string>.Failure($"Proficiency must be in [0, 1], got {Proficiency}.");
        }

        if (Uncertainty < 0.0 || Uncertainty > 1.0)
        {
            return Result<Unit, string>.Failure($"Uncertainty must be in [0, 1], got {Uncertainty}.");
        }

        if (ValidationCount < 0)
        {
            return Result<Unit, string>.Failure($"ValidationCount must be non-negative, got {ValidationCount}.");
        }

        return Result<Unit, string>.Success(Unit.Value);
    }
}