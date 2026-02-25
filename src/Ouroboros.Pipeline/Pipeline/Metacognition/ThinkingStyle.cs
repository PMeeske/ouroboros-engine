namespace Ouroboros.Pipeline.Metacognition;

/// <summary>
/// Represents a characterization of thinking style based on reasoning patterns.
/// Captures the balance between different cognitive approaches.
/// </summary>
/// <param name="StyleName">Descriptive name for this thinking style profile.</param>
/// <param name="AnalyticalScore">Score for systematic, logical analysis (0.0 to 1.0).</param>
/// <param name="CreativeScore">Score for novel, divergent thinking (0.0 to 1.0).</param>
/// <param name="SystematicScore">Score for structured, methodical approach (0.0 to 1.0).</param>
/// <param name="IntuitiveScore">Score for quick, pattern-based judgments (0.0 to 1.0).</param>
/// <param name="BiasProfile">Map of identified biases to their strength (0.0 to 1.0).</param>
public sealed record ThinkingStyle(
    string StyleName,
    double AnalyticalScore,
    double CreativeScore,
    double SystematicScore,
    double IntuitiveScore,
    ImmutableDictionary<string, double> BiasProfile)
{
    /// <summary>
    /// Creates a balanced thinking style with no detected biases.
    /// </summary>
    /// <returns>A balanced ThinkingStyle.</returns>
    public static ThinkingStyle Balanced() => new(
        StyleName: "Balanced",
        AnalyticalScore: 0.5,
        CreativeScore: 0.5,
        SystematicScore: 0.5,
        IntuitiveScore: 0.5,
        BiasProfile: ImmutableDictionary<string, double>.Empty);

    /// <summary>
    /// Creates an analytical thinking style profile.
    /// </summary>
    /// <returns>An analytically-oriented ThinkingStyle.</returns>
    public static ThinkingStyle Analytical() => new(
        StyleName: "Analytical",
        AnalyticalScore: 0.85,
        CreativeScore: 0.35,
        SystematicScore: 0.75,
        IntuitiveScore: 0.25,
        BiasProfile: ImmutableDictionary<string, double>.Empty);

    /// <summary>
    /// Creates a creative thinking style profile.
    /// </summary>
    /// <returns>A creatively-oriented ThinkingStyle.</returns>
    public static ThinkingStyle Creative() => new(
        StyleName: "Creative",
        AnalyticalScore: 0.4,
        CreativeScore: 0.9,
        SystematicScore: 0.3,
        IntuitiveScore: 0.7,
        BiasProfile: ImmutableDictionary<string, double>.Empty);

    /// <summary>
    /// Gets the dominant cognitive dimension.
    /// </summary>
    public string DominantDimension
    {
        get
        {
            var scores = new (string Name, double Score)[]
            {
                ("Analytical", AnalyticalScore),
                ("Creative", CreativeScore),
                ("Systematic", SystematicScore),
                ("Intuitive", IntuitiveScore),
            };
            return scores.MaxBy(s => s.Score).Name;
        }
    }

    /// <summary>
    /// Gets whether there are significant detected biases.
    /// </summary>
    /// <param name="threshold">The threshold above which a bias is considered significant.</param>
    /// <returns>True if any bias exceeds the threshold.</returns>
    public bool HasSignificantBiases(double threshold = 0.5)
        => BiasProfile.Values.Any(v => v > threshold);

    /// <summary>
    /// Gets the most significant biases in the profile.
    /// </summary>
    /// <param name="threshold">The minimum bias strength to include.</param>
    /// <returns>Biases that exceed the threshold, ordered by strength.</returns>
    public IEnumerable<(string Bias, double Strength)> GetSignificantBiases(double threshold = 0.3)
        => BiasProfile
            .Where(kv => kv.Value > threshold)
            .OrderByDescending(kv => kv.Value)
            .Select(kv => (kv.Key, kv.Value));

    /// <summary>
    /// Adds or updates a bias in the profile.
    /// </summary>
    /// <param name="biasName">The name of the bias.</param>
    /// <param name="strength">The strength of the bias (0.0 to 1.0).</param>
    /// <returns>A new ThinkingStyle with the updated bias.</returns>
    public ThinkingStyle WithBias(string biasName, double strength)
        => this with { BiasProfile = BiasProfile.SetItem(biasName, Math.Clamp(strength, 0.0, 1.0)) };
}