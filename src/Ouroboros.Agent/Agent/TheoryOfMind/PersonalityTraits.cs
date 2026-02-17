namespace Ouroboros.Agent.TheoryOfMind;

/// <summary>
/// Represents personality traits attributed to an agent.
/// Uses standardized trait dimensions for modeling.
/// </summary>
/// <param name="Cooperativeness">Willingness to cooperate (0.0 to 1.0)</param>
/// <param name="Predictability">How predictable the agent's behavior is (0.0 to 1.0)</param>
/// <param name="Competence">Assessed competence level (0.0 to 1.0)</param>
/// <param name="CustomTraits">Additional custom trait dimensions</param>
public sealed record PersonalityTraits(
    double Cooperativeness,
    double Predictability,
    double Competence,
    Dictionary<string, double> CustomTraits)
{
    /// <summary>
    /// Creates default personality traits (neutral values).
    /// </summary>
    /// <returns>Default personality traits</returns>
    public static PersonalityTraits Default() => new(
        0.5,
        0.5,
        0.5,
        new Dictionary<string, double>());

    /// <summary>
    /// Updates a specific trait value.
    /// </summary>
    /// <param name="traitName">Name of the custom trait</param>
    /// <param name="value">Trait value (0.0 to 1.0)</param>
    /// <returns>Updated personality traits</returns>
    public PersonalityTraits WithTrait(string traitName, double value)
    {
        Dictionary<string, double> updated = new(this.CustomTraits)
        {
            [traitName] = Math.Clamp(value, 0.0, 1.0)
        };

        return this with { CustomTraits = updated };
    }
}