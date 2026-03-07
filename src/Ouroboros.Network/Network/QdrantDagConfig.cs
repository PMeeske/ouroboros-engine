using Ouroboros.Providers.Configuration;

namespace Ouroboros.Network;

/// <summary>
/// Configuration for Qdrant DAG storage.
/// </summary>
[Obsolete("Use QdrantSettings + IQdrantCollectionRegistry from DI instead.")]
/// <param name="Endpoint">Qdrant server endpoint (e.g., <see cref="DefaultEndpoints.QdrantGrpc"/>).</param>
/// <param name="NodesCollection">Collection name for MonadNodes.</param>
/// <param name="EdgesCollection">Collection name for TransitionEdges.</param>
/// <param name="VectorSize">Embedding vector dimension (default 1536 for OpenAI).</param>
/// <param name="UseHttps">Whether to use HTTPS connection.</param>
public sealed record QdrantDagConfig(
    string Endpoint = DefaultEndpoints.QdrantGrpc,
    string NodesCollection = "ouroboros_dag_nodes",
    string EdgesCollection = "ouroboros_dag_edges",
    int VectorSize = 1536,
    bool UseHttps = false);