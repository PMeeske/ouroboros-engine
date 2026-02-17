namespace Ouroboros.Pipeline.WorldModel;

/// <summary>
/// Represents an observation in the world state with associated metadata.
/// </summary>
/// <param name="Value">The observed value.</param>
/// <param name="Confidence">Confidence score between 0.0 and 1.0.</param>
/// <param name="Timestamp">When the observation was recorded.</param>
public sealed record Observation(
    object Value,
    double Confidence,
    DateTime Timestamp)
{
    /// <summary>
    /// Creates a new observation with the current timestamp.
    /// </summary>
    /// <param name="value">The observed value.</param>
    /// <param name="confidence">Confidence score between 0.0 and 1.0.</param>
    /// <returns>A new observation.</returns>
    public static Observation Create(object value, double confidence)
    {
        ArgumentNullException.ThrowIfNull(value);

        double clampedConfidence = Math.Clamp(confidence, 0.0, 1.0);
        return new Observation(value, clampedConfidence, DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a new observation with full confidence.
    /// </summary>
    /// <param name="value">The observed value.</param>
    /// <returns>A new observation with confidence 1.0.</returns>
    public static Observation Certain(object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new Observation(value, 1.0, DateTime.UtcNow);
    }

    /// <summary>
    /// Gets the value as a specific type if possible.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <returns>Option containing the typed value if cast succeeds.</returns>
    public Option<T> GetValueAs<T>()
    {
        return Value is T typedValue
            ? Option<T>.Some(typedValue)
            : Option<T>.None();
    }
}