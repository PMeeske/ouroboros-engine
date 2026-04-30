// <copyright file="VectorGraphFeedbackLoop.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Network;

/// <summary>
/// Configuration for the vector graph feedback loop.
/// </summary>
/// <param name="DivergenceThreshold">Threshold for classifying nodes as sources or sinks.</param>
/// <param name="RotationThreshold">Threshold for classifying nodes as cyclic.</param>
/// <param name="MaxModificationsPerCycle">Maximum number of graph modifications per cycle.</param>
/// <param name="AutoPersist">Whether to automatically persist changes to Qdrant.</param>
public sealed record FeedbackLoopConfig(
    float DivergenceThreshold = 0.5f,
    float RotationThreshold = 0.3f,
    int MaxModificationsPerCycle = 10,
    bool AutoPersist = true);

/// <summary>
/// Result of a feedback loop execution cycle.
/// </summary>
/// <param name="NodesAnalyzed">Total number of nodes analyzed.</param>
/// <param name="NodesModified">Number of nodes that were modified.</param>
/// <param name="SourceNodes">Number of nodes identified as semantic sources.</param>
/// <param name="SinkNodes">Number of nodes identified as semantic sinks.</param>
/// <param name="CyclicNodes">Number of nodes with high rotation (reasoning cycles).</param>
/// <param name="Duration">Time taken for the cycle.</param>
public sealed record FeedbackResult(
    int NodesAnalyzed,
    int NodesModified,
    int SourceNodes,
    int SinkNodes,
    int CyclicNodes,
    TimeSpan Duration);

/// <summary>
/// Implements a closed feedback loop for neuro-symbolic graph reasoning.
/// Analyzes vector field properties, feeds results to MeTTa for symbolic reasoning,
/// applies suggested modifications, and re-embeds updated nodes.
/// </summary>
public sealed partial class VectorGraphFeedbackLoop
{
    private const int DefaultEmbeddingDimension = 384;

    private readonly QdrantDagStore _store;
    private readonly IMeTTaEngine _mettaEngine;
    private readonly IEmbeddingModel _embeddingModel;
    private readonly FeedbackLoopConfig _config;
    private readonly Dictionary<Guid, float[]> _embeddingCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorGraphFeedbackLoop"/> class.
    /// </summary>
    /// <param name="store">Qdrant DAG store for persistence.</param>
    /// <param name="mettaEngine">MeTTa engine for symbolic reasoning.</param>
    /// <param name="embeddingModel">Embedding model for re-embedding modified nodes.</param>
    /// <param name="config">Configuration for the feedback loop.</param>
    public VectorGraphFeedbackLoop(
        QdrantDagStore store,
        IMeTTaEngine mettaEngine,
        IEmbeddingModel embeddingModel,
        FeedbackLoopConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        ArgumentNullException.ThrowIfNull(mettaEngine);
        _mettaEngine = mettaEngine;
        ArgumentNullException.ThrowIfNull(embeddingModel);
        _embeddingModel = embeddingModel;
        _config = config ?? new FeedbackLoopConfig();
        _embeddingCache = new Dictionary<Guid, float[]>();
    }

    /// <summary>
    /// Executes a complete feedback cycle: analyze, reason, modify, persist.
    /// </summary>
    /// <param name="dag">The MerkleDag to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the feedback cycle statistics.</returns>
    public async Task<Result<FeedbackResult, string>> ExecuteCycleAsync(
        MerkleDag dag,
        CancellationToken ct = default)
    {
        if (dag == null)
        {
            return Result<FeedbackResult, string>.Failure("DAG cannot be null");
        }

        var startTime = DateTime.UtcNow;

        try
        {
            // Step 1: Build embedding cache from Qdrant
            await BuildEmbeddingCacheAsync(dag, ct).ConfigureAwait(false);

            // Step 2: Compute vector field properties
            var divergenceMap = VectorFieldOperations.ComputeAllDivergences(dag, GetCachedEmbedding);
            var rotationMap = VectorFieldOperations.ComputeAllRotations(dag, GetCachedEmbedding);

            // Step 3: Analyze and classify nodes
            var classification = ClassifyNodes(divergenceMap, rotationMap);

            // Step 4: Feed analysis results to MeTTa
            await FeedAnalysisToMeTTaAsync(classification, ct).ConfigureAwait(false);

            // Step 5: Query MeTTa for suggested modifications
            var modificationsResult = await QueryMeTTaForModificationsAsync(ct).ConfigureAwait(false);
            if (modificationsResult.IsFailure)
            {
                return Result<FeedbackResult, string>.Failure(
                    $"Failed to query MeTTa for modifications: {modificationsResult.Error}");
            }

            // Step 6: Apply modifications to DAG
            var modifications = ParseModifications(modificationsResult.Value);
            var modifiedNodes = new HashSet<Guid>();
            await VectorGraphFeedbackLoop.ApplyModificationsAsync(dag, modifications, modifiedNodes, ct, _config.MaxModificationsPerCycle).ConfigureAwait(false);

            // Step 7: Re-embed and persist modified nodes
            if (_config.AutoPersist && modifiedNodes.Count > 0)
            {
                await ReEmbedAndPersistAsync(dag, modifiedNodes, ct).ConfigureAwait(false);
            }

            // Step 8: Build and return result
            var duration = DateTime.UtcNow - startTime;
            var result = new FeedbackResult(
                NodesAnalyzed: dag.NodeCount,
                NodesModified: modifiedNodes.Count,
                SourceNodes: classification.Sources.Count,
                SinkNodes: classification.Sinks.Count,
                CyclicNodes: classification.Cyclic.Count,
                Duration: duration);

            return Result<FeedbackResult, string>.Success(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<FeedbackResult, string>.Failure($"Feedback cycle failed: {ex.Message}");
        }
    }

    private async Task BuildEmbeddingCacheAsync(MerkleDag dag, CancellationToken ct)
    {
        _embeddingCache.Clear();

        // Try to load embeddings from Qdrant for existing nodes
        foreach (var node in dag.Nodes.Values)
        {
            try
            {
                // Generate embedding for the node
                var semanticText = $"{node.TypeName}: {node.PayloadJson}";
                var embedding = await _embeddingModel.CreateEmbeddingsAsync(semanticText, ct).ConfigureAwait(false);
                _embeddingCache[node.Id] = embedding;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                System.Diagnostics.Trace.TraceWarning(
                    $"[FeedbackLoop] Embedding failed for node {node.Id}: {ex.Message}. Using zero vector.");
                _embeddingCache[node.Id] = new float[DefaultEmbeddingDimension];
            }
        }
    }

    private float[] GetCachedEmbedding(Guid nodeId)
    {
        return _embeddingCache.TryGetValue(nodeId, out var embedding)
            ? embedding
            : new float[DefaultEmbeddingDimension];
    }

    private NodeClassification ClassifyNodes(
        IReadOnlyDictionary<Guid, float> divergenceMap,
        IReadOnlyDictionary<Guid, float> rotationMap)
    {
        var sources = new List<Guid>();
        var sinks = new List<Guid>();
        var neutral = new List<Guid>();
        var cyclic = new List<Guid>();

        foreach (var nodeId in divergenceMap.Keys)
        {
            var divergence = divergenceMap[nodeId];
            var rotation = rotationMap.TryGetValue(nodeId, out var rot) ? rot : 0f;

            // Classify by divergence
            if (divergence > _config.DivergenceThreshold)
            {
                sources.Add(nodeId);
            }
            else if (divergence < -_config.DivergenceThreshold)
            {
                sinks.Add(nodeId);
            }
            else
            {
                neutral.Add(nodeId);
            }

            // Classify by rotation
            if (rotation > _config.RotationThreshold)
            {
                cyclic.Add(nodeId);
            }
        }

        return new NodeClassification(sources, sinks, neutral, cyclic);
    }

    private async Task FeedAnalysisToMeTTaAsync(NodeClassification classification, CancellationToken ct)
    {
        // Add facts about semantic sources
        foreach (var nodeId in classification.Sources)
        {
            var fact = $"!(semantic-source \"{EscapeMeTTaString(nodeId.ToString())}\")";
            await _mettaEngine.AddFactAsync(fact, ct).ConfigureAwait(false);
        }

        // Add facts about semantic sinks
        foreach (var nodeId in classification.Sinks)
        {
            var fact = $"!(semantic-sink \"{EscapeMeTTaString(nodeId.ToString())}\")";
            await _mettaEngine.AddFactAsync(fact, ct).ConfigureAwait(false);
        }

        // Add facts about neutral nodes
        foreach (var nodeId in classification.Neutral)
        {
            var fact = $"!(semantic-neutral \"{EscapeMeTTaString(nodeId.ToString())}\")";
            await _mettaEngine.AddFactAsync(fact, ct).ConfigureAwait(false);
        }

        // Add facts about cyclic nodes
        foreach (var nodeId in classification.Cyclic)
        {
            var fact = $"!(reasoning-cycle \"{EscapeMeTTaString(nodeId.ToString())}\")";
            await _mettaEngine.AddFactAsync(fact, ct).ConfigureAwait(false);
        }

        // Add symbolic rules for graph modification reasoning
        var rule = @"
!(= (suggest-edge-strengthen $source $sink)
    (if (and (semantic-source $source) (semantic-sink $sink))
        (strengthen-edge $source $sink)))

!(= (suggest-edge-weaken $node)
    (if (reasoning-cycle $node)
        (weaken-outgoing-edges $node)))

!(= (suggest-node-merge $sink1 $sink2)
    (if (and (semantic-sink $sink1) (semantic-sink $sink2))
        (merge-sinks $sink1 $sink2)))
";
        await _mettaEngine.ApplyRuleAsync(rule, ct).ConfigureAwait(false);
    }

    private async Task<Result<string, string>> QueryMeTTaForModificationsAsync(CancellationToken ct)
    {
        // Query for suggested modifications
        var query = "!(match &self (suggest-edge-strengthen $s $t) (strengthen $s $t))";
        return await _mettaEngine.ExecuteQueryAsync(query, ct).ConfigureAwait(false);
    }

    internal static List<GraphModification> ParseModifications(string mettaResult)
    {
        var modifications = new List<GraphModification>();

        if (string.IsNullOrWhiteSpace(mettaResult))
        {
            return modifications;
        }

        // Normalize: strip outer Python-style list brackets (e.g. "[[...]]" or "[...]")
        var normalized = mettaResult.Trim();
        while (normalized.StartsWith('[') && normalized.EndsWith(']'))
        {
            normalized = normalized[1..^1].Trim();
        }

        // Empty result after stripping brackets, or just "()"
        if (string.IsNullOrWhiteSpace(normalized) || normalized == "()")
        {
            return modifications;
        }

        // Extract all top-level S-expression forms: (operation arg1 arg2 ...)
        // Handles both space-separated S-exprs (HyperonMeTTaEngine) and
        // comma-separated results from Python subprocess output.
        foreach (Match match in SExpressionPattern().Matches(normalized))
        {
            var sExpr = match.Value.Trim();
            var parsed = ParseSingleModification(sExpr);
            if (parsed != null)
            {
                modifications.AddRange(parsed);
            }
        }

        return modifications;
    }

    /// <summary>
    /// Parses a single S-expression modification directive into one or more
    /// <see cref="GraphModification"/> entries.
    /// </summary>
    /// <remarks>
    /// Supported forms (matching the MeTTa rules defined in FeedAnalysisToMeTTaAsync):
    ///   (strengthen "source-guid" "target-guid")
    ///   (strengthen-edge "source-guid" "target-guid")
    ///   (weaken-outgoing-edges "node-guid")
    ///   (merge-sinks "sink1-guid" "sink2-guid")
    /// GUIDs may appear with or without surrounding double-quotes.
    /// </remarks>
    private static List<GraphModification>? ParseSingleModification(string sExpr)
    {
        if (string.IsNullOrWhiteSpace(sExpr))
        {
            return null;
        }

        // Strip outer parens
        var inner = sExpr.Trim();
        if (inner.StartsWith('(') && inner.EndsWith(')'))
        {
            inner = inner[1..^1].Trim();
        }

        if (string.IsNullOrWhiteSpace(inner))
        {
            return null;
        }

        // Tokenize: split on whitespace, but keep quoted strings intact
        var tokens = TokenizeSExpression(inner);
        if (tokens.Count < 2)
        {
            return null;
        }

        var operation = tokens[0].ToLowerInvariant();
        var guidArgs = new List<Guid>();

        for (int i = 1; i < tokens.Count; i++)
        {
            var raw = tokens[i].Trim('"');
            if (Guid.TryParse(raw, out var guid))
            {
                guidArgs.Add(guid);
            }
        }

        if (guidArgs.Count == 0)
        {
            return null;
        }

        var results = new List<GraphModification>();

        switch (operation)
        {
            case "strengthen":
            case "strengthen-edge":
                // Both source and target nodes are affected
                foreach (var guid in guidArgs)
                {
                    results.Add(new GraphModification(guid, "strengthen"));
                }

                break;

            case "weaken-outgoing-edges":
            case "weaken":
                // The single node whose outgoing edges should be weakened
                results.Add(new GraphModification(guidArgs[0], "weaken"));
                break;

            case "merge-sinks":
            case "merge":
                // Both sink nodes participate in the merge
                foreach (var guid in guidArgs)
                {
                    results.Add(new GraphModification(guid, "merge"));
                }

                break;

            default:
                // Unknown operation type -- still record the nodes so the
                // feedback loop can track them as modified.
                foreach (var guid in guidArgs)
                {
                    results.Add(new GraphModification(guid, operation));
                }

                break;
        }

        return results.Count > 0 ? results : null;
    }

    /// <summary>
    /// Tokenizes a MeTTa S-expression body, splitting on whitespace while
    /// preserving quoted string tokens (e.g. "some-guid") as single tokens.
    /// </summary>
    private static List<string> TokenizeSExpression(string input)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuote = false;

        foreach (char c in input)
        {
            if (c == '"')
            {
                inQuote = !inQuote;
                current.Append(c);
            }
            else if (char.IsWhiteSpace(c) && !inQuote)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }

    /// <summary>
    /// Matches top-level parenthesized S-expressions, handling nested parens.
    /// </summary>
    [GeneratedRegex(@"\([^()]*(?:\([^()]*\))*[^()]*\)")]
    private static partial Regex SExpressionPattern();

    internal static async Task ApplyModificationsAsync(
        MerkleDag dag,
        List<GraphModification> modifications,
        HashSet<Guid> modifiedNodes,
        CancellationToken ct,
        int maxModifications = 10)
    {
        int appliedCount = 0;

        foreach (var modification in modifications)
        {
            if (appliedCount >= maxModifications)
            {
                break;
            }

            ct.ThrowIfCancellationRequested();

            bool applied = modification.ModificationType switch
            {
                "strengthen" => ApplyStrengthen(dag, modification.NodeId, modifiedNodes),
                "weaken" => ApplyWeaken(dag, modification.NodeId, modifiedNodes),
                "merge" => ApplyMerge(dag, modification.NodeId, modifiedNodes),
                _ => ApplyDefault(dag, modification.NodeId, modifiedNodes)
            };

            if (applied)
            {
                appliedCount++;
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Increases the weight (confidence) of all outgoing edges from the specified node.
    /// Each edge's confidence is multiplied by 1.1, capped at 1.0.
    /// </summary>
    /// <param name="dag">The MerkleDAG to modify.</param>
    /// <param name="nodeId">The node whose outgoing edges to strengthen.</param>
    /// <param name="modifiedNodes">Set tracking which nodes were modified for re-embedding.</param>
    /// <returns>True if any edge was modified, false otherwise.</returns>
    internal static bool ApplyStrengthen(MerkleDag dag, Guid nodeId, HashSet<Guid> modifiedNodes)
    {
        ArgumentNullException.ThrowIfNull(dag);
        ArgumentNullException.ThrowIfNull(modifiedNodes);

        var outgoingEdges = dag.GetEdgesFrom(nodeId);
        if (outgoingEdges.Count == 0)
        {
            System.Diagnostics.Trace.TraceInformation(
                "[FeedbackLoop] Strengthen: Node {0} has no outgoing edges.", nodeId);
            return false;
        }

        bool anyModified = false;

        foreach (var (edgeId, edge) in outgoingEdges)
        {
            var currentConfidence = edge.Confidence ?? 0.5;
            var newConfidence = Math.Min(currentConfidence * 1.1, 1.0);

            if (Math.Abs(newConfidence - currentConfidence) < double.Epsilon)
            {
                continue;
            }

            var updatedEdge = new TransitionEdge(
                edge.Id,
                edge.InputIds,
                edge.OutputId,
                edge.OperationName,
                edge.OperationSpecJson,
                edge.CreatedAt,
                confidence: newConfidence,
                durationMs: edge.DurationMs);

            var result = dag.UpdateEdge(updatedEdge);
            if (result.IsSuccess)
            {
                anyModified = true;
                modifiedNodes.Add(edge.OutputId);
            }
        }

        if (anyModified)
        {
            modifiedNodes.Add(nodeId);
        }

        return anyModified;
    }

    /// <summary>
    /// Reduces the weight (confidence) of all outgoing edges from the specified node.
    /// Each edge's confidence is multiplied by 0.8. Edges below 0.05 confidence are pruned.
    /// </summary>
    /// <param name="dag">The MerkleDAG to modify.</param>
    /// <param name="nodeId">The node whose outgoing edges to weaken.</param>
    /// <param name="modifiedNodes">Set tracking which nodes were modified for re-embedding.</param>
    /// <returns>True if any edge was modified or removed, false otherwise.</returns>
    internal static bool ApplyWeaken(MerkleDag dag, Guid nodeId, HashSet<Guid> modifiedNodes)
    {
        ArgumentNullException.ThrowIfNull(dag);
        ArgumentNullException.ThrowIfNull(modifiedNodes);

        var outgoingEdges = dag.GetEdgesFrom(nodeId);
        if (outgoingEdges.Count == 0)
        {
            return false;
        }

        bool anyModified = false;

        foreach (var (edgeId, edge) in outgoingEdges)
        {
            var currentConfidence = edge.Confidence ?? 0.5;
            var newConfidence = currentConfidence * 0.8;

            if (newConfidence < 0.05)
            {
                // Prune edges below threshold
                var removeResult = dag.RemoveEdge(edgeId);
                if (removeResult.IsSuccess)
                {
                    anyModified = true;
                    modifiedNodes.Add(edge.OutputId);
                }
            }
            else
            {
                var updatedEdge = new TransitionEdge(
                    edge.Id,
                    edge.InputIds,
                    edge.OutputId,
                    edge.OperationName,
                    edge.OperationSpecJson,
                    edge.CreatedAt,
                    confidence: newConfidence,
                    durationMs: edge.DurationMs);

                var result = dag.UpdateEdge(updatedEdge);
                if (result.IsSuccess)
                {
                    anyModified = true;
                    modifiedNodes.Add(edge.OutputId);
                }
            }
        }

        if (anyModified)
        {
            modifiedNodes.Add(nodeId);
        }

        return anyModified;
    }

    /// <summary>
    /// Merges the specified node with its highest-weight outgoing edge target.
    /// Combines payloads, redirects inbound edges, and removes the merge partner.
    /// </summary>
    /// <param name="dag">The MerkleDAG to modify.</param>
    /// <param name="nodeId">The node to merge (absorbs its highest-weight neighbor).</param>
    /// <param name="modifiedNodes">Set tracking which nodes were modified for re-embedding.</param>
    /// <returns>True if the merge was performed, false otherwise.</returns>
    internal static bool ApplyMerge(MerkleDag dag, Guid nodeId, HashSet<Guid> modifiedNodes)
    {
        ArgumentNullException.ThrowIfNull(dag);
        ArgumentNullException.ThrowIfNull(modifiedNodes);

        var nodeOption = dag.GetNode(nodeId);
        if (!nodeOption.HasValue)
        {
            return false;
        }

        var outgoingEdges = dag.GetEdgesFrom(nodeId);
        if (outgoingEdges.Count == 0)
        {
            return false;
        }

        // Find highest-weight merge partner
        var bestEdge = outgoingEdges.Values
            .OrderByDescending(e => e.Confidence ?? 0.5)
            .First();

        var partnerId = bestEdge.OutputId;
        var partnerOption = dag.GetNode(partnerId);
        if (!partnerOption.HasValue)
        {
            return false;
        }

        var node = nodeOption.Value!;
        var partner = partnerOption.Value!;

        // Combine payloads via JSON merge
        var mergedPayload = MergePayloads(node.PayloadJson, partner.PayloadJson);

        // Update this node with merged payload
        var updatedNode = new MonadNode(
            node.Id,
            node.TypeName,
            mergedPayload,
            node.CreatedAt,
            node.ParentIds);

        var updateResult = dag.UpdateNode(updatedNode);
        if (updateResult.IsFailure)
        {
            return false;
        }

        // Redirect all inbound edges of the partner to point to this node instead
        var partnerIncomingEdges = dag.GetIncomingEdges(partnerId).ToList();
        foreach (var incomingEdge in partnerIncomingEdges)
        {
            // Create a new edge with the same properties but output pointing to nodeId
            var redirectedEdge = new TransitionEdge(
                incomingEdge.Id,
                incomingEdge.InputIds,
                nodeId,
                incomingEdge.OperationName,
                incomingEdge.OperationSpecJson,
                incomingEdge.CreatedAt,
                confidence: incomingEdge.Confidence,
                durationMs: incomingEdge.DurationMs);

            // Remove old edge and add redirected one with new output target
            dag.RemoveEdge(incomingEdge.Id);
            dag.AddEdge(redirectedEdge);
        }

        // Remove the edge from nodeId to partnerId
        dag.RemoveEdge(bestEdge.Id);

        // Remove the merge partner node
        dag.RemoveNode(partnerId);

        // Track modified nodes for re-embedding
        modifiedNodes.Add(nodeId);
        modifiedNodes.Add(partnerId);

        return true;
    }

    /// <summary>
    /// Default handler for unknown modification types — just tracks the node as modified.
    /// </summary>
    /// <param name="dag">The MerkleDAG (unused for default handler).</param>
    /// <param name="nodeId">The node ID to track.</param>
    /// <param name="modifiedNodes">Set tracking which nodes were modified.</param>
    /// <returns>Always returns true.</returns>
    internal static bool ApplyDefault(MerkleDag dag, Guid nodeId, HashSet<Guid> modifiedNodes)
    {
        ArgumentNullException.ThrowIfNull(modifiedNodes);

        modifiedNodes.Add(nodeId);
        return true;
    }

    /// <summary>
    /// Merges two JSON payloads by combining their top-level properties.
    /// Properties from the secondary payload override those in the primary.
    /// </summary>
    /// <param name="primaryJson">The primary payload JSON.</param>
    /// <param name="secondaryJson">The secondary payload JSON (overrides primary on conflict).</param>
    /// <returns>A merged JSON string.</returns>
    private static string MergePayloads(string primaryJson, string secondaryJson)
    {
        try
        {
            // Use simple JSON merge: parse both, combine properties, serialize
            using var primaryDoc = System.Text.Json.JsonDocument.Parse(primaryJson);
            using var secondaryDoc = System.Text.Json.JsonDocument.Parse(secondaryJson);

            var merged = new Dictionary<string, object?>();

            // Copy primary properties
            if (primaryDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var prop in primaryDoc.RootElement.EnumerateObject())
                {
                    merged[prop.Name] = DeserializeJsonElement(prop.Value);
                }
            }

            // Overlay secondary properties (secondary takes precedence on conflict)
            if (secondaryDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var prop in secondaryDoc.RootElement.EnumerateObject())
                {
                    merged[prop.Name] = DeserializeJsonElement(prop.Value);
                }
            }

            return System.Text.Json.JsonSerializer.Serialize(merged);
        }
        catch (System.Text.Json.JsonException)
        {
            // If either payload isn't valid JSON, concatenate them
            return $"{primaryJson}+{secondaryJson}";
        }
    }

    private static object? DeserializeJsonElement(System.Text.Json.JsonElement element)
    {
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => element.GetString(),
            System.Text.Json.JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.Null => null,
            System.Text.Json.JsonValueKind.Array => element.GetRawText(),
            System.Text.Json.JsonValueKind.Object => element.GetRawText(),
            _ => element.GetRawText()
        };
    }

    private async Task ReEmbedAndPersistAsync(
        MerkleDag dag,
        HashSet<Guid> modifiedNodeIds,
        CancellationToken ct)
    {
        foreach (var nodeId in modifiedNodeIds)
        {
            var nodeOption = dag.GetNode(nodeId);
            if (!nodeOption.HasValue)
            {
                continue;
            }

            var node = nodeOption.Value!;

            // Re-generate embedding
            var semanticText = $"{node.TypeName}: {node.PayloadJson}";
            var newEmbedding = await _embeddingModel.CreateEmbeddingsAsync(semanticText, ct).ConfigureAwait(false);

            // Update cache
            _embeddingCache[nodeId] = newEmbedding;

            // Persist to Qdrant
            await _store.SaveNodeAsync(node, ct).ConfigureAwait(false);
        }
    }

    private static string EscapeMeTTaString(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    private sealed record NodeClassification(
        List<Guid> Sources,
        List<Guid> Sinks,
        List<Guid> Neutral,
        List<Guid> Cyclic);

    /// <summary>
    /// Represents a single graph modification directive parsed from MeTTa output.
    /// </summary>
    /// <param name="NodeId">The node ID targeted by this modification.</param>
    /// <param name="ModificationType">The type of modification (strengthen, weaken, merge, or unknown).</param>
    internal sealed record GraphModification(Guid NodeId, string ModificationType);
}
