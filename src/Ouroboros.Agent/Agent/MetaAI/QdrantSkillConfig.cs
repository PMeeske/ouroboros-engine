namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for Qdrant skill storage.
/// </summary>
[Obsolete("Use QdrantSettings + IQdrantCollectionRegistry from DI instead.")]
public sealed record QdrantSkillConfig(
    string ConnectionString = "http://localhost:6334",
    string CollectionName = "ouroboros_skills",
    bool AutoSave = true,
    int VectorSize = 1536);