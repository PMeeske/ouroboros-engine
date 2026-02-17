namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for Qdrant skill storage.
/// </summary>
public sealed record QdrantSkillConfig(
    string ConnectionString = "http://localhost:6334",
    string CollectionName = "ouroboros_skills",
    bool AutoSave = true,
    int VectorSize = 1536);