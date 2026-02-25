namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Interface for skill composition capabilities.
/// </summary>
public interface ISkillComposer
{
    /// <summary>
    /// Composes multiple skills into a higher-level skill.
    /// </summary>
    Task<Result<Skill, string>> ComposeSkillsAsync(
        string compositeName,
        string description,
        List<string> componentSkillNames,
        SkillCompositionConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Suggests skill compositions based on usage patterns.
    /// </summary>
    Task<List<(List<string> skills, double score)>> SuggestCompositionsAsync(
        int maxSuggestions = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Decomposes a composite skill into its components.
    /// </summary>
    Result<List<Skill>, string> DecomposeSkill(string compositeName);
}