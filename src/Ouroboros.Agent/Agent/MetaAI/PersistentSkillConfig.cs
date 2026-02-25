namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for persistent skill storage.
/// </summary>
public sealed record PersistentSkillConfig(
    string StoragePath = "skills.json",
    bool UseVectorStore = true,
    string CollectionName = "ouroboros_skills",
    bool AutoSave = true);