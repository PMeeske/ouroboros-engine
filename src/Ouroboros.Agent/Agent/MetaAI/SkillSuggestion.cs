namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a skill suggestion for DSL autocompletion.
/// </summary>
public sealed record SkillSuggestion(
    string TokenName,
    Skill Skill,
    double RelevanceScore,
    string UsageExample);