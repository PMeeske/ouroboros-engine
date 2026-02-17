namespace Ouroboros.Network;

/// <summary>
/// Configuration for Qdrant DAG storage.
/// </summary>
/// <param name="Endpoint">Qdrant server endpoint (e.g., "http://localhost:6334").</param>
/// <param name="NodesCollection">Collection name for MonadNodes.</param>
/// <param name="EdgesCollection">Collection name for TransitionEdges.</param>
/// <param name="VectorSize">Embedding vector dimension (default 1536 for OpenAI).</param>
/// <param name="UseHttps">Whether to use HTTPS connection.</param>
public sealed record QdrantDagConfig(
    string Endpoint = "http://localhost:6334",
    string NodesCollection = "ouroboros_dag_nodes",
    string EdgesCollection = "ouroboros_dag_edges",
    int VectorSize = 1536,
    bool UseHttps = false);