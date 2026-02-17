namespace Ouroboros.Network;

/// <summary>
/// Internal serialization data for MerkleDag.
/// </summary>
internal sealed record DagSerializationData(NodeData[] Nodes, EdgeData[] Edges);