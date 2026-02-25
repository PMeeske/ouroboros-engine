namespace Ouroboros.Pipeline.Learning;

/// <summary>
/// Represents a parameter update computed by online learning.
/// Captures the full update context including gradient and confidence.
/// </summary>
/// <param name="ParameterName">The name of the parameter being updated.</param>
/// <param name="OldValue">The previous value of the parameter.</param>
/// <param name="NewValue">The computed new value for the parameter.</param>
/// <param name="Gradient">The gradient or direction of the update.</param>
/// <param name="Confidence">Confidence in this update, in range [0, 1].</param>
public sealed record LearningUpdate(
    string ParameterName,
    double OldValue,
    double NewValue,
    double Gradient,
    double Confidence)
{
    /// <summary>
    /// Computes the magnitude of this update.
    /// </summary>
    public double Magnitude => Math.Abs(NewValue - OldValue);

    /// <summary>
    /// Creates a learning update with computed new value from gradient.
    /// </summary>
    /// <param name="parameterName">Name of the parameter.</param>
    /// <param name="currentValue">Current parameter value.</param>
    /// <param name="gradient">Computed gradient.</param>
    /// <param name="learningRate">Learning rate for scaling.</param>
    /// <param name="confidence">Confidence in the update.</param>
    /// <returns>A new LearningUpdate instance.</returns>
    public static LearningUpdate FromGradient(
        string parameterName,
        double currentValue,
        double gradient,
        double learningRate,
        double confidence = 1.0)
    {
        var delta = -learningRate * gradient; // Gradient descent: move opposite to gradient
        var newValue = currentValue + delta;
        return new LearningUpdate(
            parameterName,
            currentValue,
            newValue,
            gradient,
            Math.Clamp(confidence, 0.0, 1.0));
    }

    /// <summary>
    /// Creates a scaled version of this update.
    /// </summary>
    /// <param name="scale">The scaling factor to apply.</param>
    /// <returns>A new LearningUpdate with scaled values.</returns>
    public LearningUpdate Scale(double scale)
    {
        var scaledDelta = (NewValue - OldValue) * scale;
        return this with
        {
            NewValue = OldValue + scaledDelta,
            Gradient = Gradient * scale,
        };
    }

    /// <summary>
    /// Merges this update with another by averaging.
    /// </summary>
    /// <param name="other">The other update to merge with.</param>
    /// <returns>A merged LearningUpdate.</returns>
    public LearningUpdate MergeWith(LearningUpdate other)
    {
        if (ParameterName != other.ParameterName)
        {
            throw new ArgumentException($"Cannot merge updates for different parameters: {ParameterName} vs {other.ParameterName}");
        }

        var totalConfidence = Confidence + other.Confidence;
        var w1 = Confidence / totalConfidence;
        var w2 = other.Confidence / totalConfidence;

        return new LearningUpdate(
            ParameterName,
            OldValue,
            (NewValue * w1) + (other.NewValue * w2),
            (Gradient * w1) + (other.Gradient * w2),
            Math.Max(Confidence, other.Confidence));
    }
}