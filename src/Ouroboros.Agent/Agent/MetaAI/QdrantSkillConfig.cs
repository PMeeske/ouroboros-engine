using Ouroboros.Providers.Configuration;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for Qdrant skill storage.
/// </summary>
[Obsolete("Use QdrantSettings + IQdrantCollectionRegistry from DI instead.")]
public sealed record QdrantSkillConfig(
    string ConnectionString = DefaultEndpoints.QdrantGrpc,
    string CollectionName = "ouroboros_skills",
    bool AutoSave = true,
    int VectorSize = 1536);