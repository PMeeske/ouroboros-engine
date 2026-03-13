// <copyright file="VectorGraphFeedbackLoop.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;
using Ouroboros.Abstractions;
using Ouroboros.Domain;
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
            await BuildEmbeddingCacheAsync(dag, ct);

            // Step 2: Compute vector field properties
            var divergenceMap = VectorFieldOperations.ComputeAllDivergences(dag, GetCachedEmbedding);
            var rotationMap = VectorFieldOperations.ComputeAllRotations(dag, GetCachedEmbedding);

            // Step 3: Analyze and classify nodes
            var classification = ClassifyNodes(divergenceMap, rotationMap);

            // Step 4: Feed analysis results to MeTTa
            await FeedAnalysisToMeTTaAsync(classification, ct);

            // Step 5: Query MeTTa for suggested modifications
            var modificationsResult = await QueryMeTTaForModificationsAsync(ct);
            if (modificationsResult.IsFailure)
            {
                return Result<FeedbackResult, string>.Failure(
                    $"Failed to query MeTTa for modifications: {modificationsResult.Error}");
            }

            // Step 6: Apply modifications to DAG
            var modifications = ParseModifications(modificationsResult.Value);
            var modifiedNodes = new HashSet<Guid>();
            await ApplyModificationsAsync(dag, modifications, modifiedNodes, ct);

            // Step 7: Re-embed and persist modified nodes
            if (_config.AutoPersist && modifiedNodes.Count > 0)
            {
                await ReEmbedAndPersistAsync(dag, modifiedNodes, ct);
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
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
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
                var embedding = await _embeddingModel.CreateEmbeddingsAsync(semanticText, ct);
                _embeddingCache[node.Id] = embedding;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
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
            await _mettaEngine.AddFactAsync(fact, ct);
        }

        // Add facts about semantic sinks
        foreach (var nodeId in classification.Sinks)
        {
            var fact = $"!(semantic-sink \"{EscapeMeTTaString(nodeId.ToString())}\")";
            await _mettaEngine.AddFactAsync(fact, ct);
        }

        // Add facts about neutral nodes
        foreach (var nodeId in classification.Neutral)
        {
            var fact = $"!(semantic-neutral \"{EscapeMeTTaString(nodeId.ToString())}\")";
            await _mettaEngine.AddFactAsync(fact, ct);
        }

        // Add facts about cyclic nodes
        foreach (var nodeId in classification.Cyclic)
        {
            var fact = $"!(reasoning-cycle \"{EscapeMeTTaString(nodeId.ToString())}\")";
            await _mettaEngine.AddFactAsync(fact, ct);
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
        await _mettaEngine.ApplyRuleAsync(rule, ct);
    }

    private async Task<Result<string, string>> QueryMeTTaForModificationsAsync(CancellationToken ct)
    {
        // Query for suggested modifications
        var query = "!(match &self (suggest-edge-strengthen $s $t) (strengthen $s $t))";
        return await _mettaEngine.ExecuteQueryAsync(query, ct);
    }

    private static List<GraphModification> ParseModifications(string mettaResult)
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

    private async Task ApplyModificationsAsync(
        MerkleDag dag,
        List<GraphModification> modifications,
        HashSet<Guid> modifiedNodes,
        CancellationToken ct)
    {
        int appliedCount = 0;

        foreach (var modification in modifications)
        {
            if (appliedCount >= _config.MaxModificationsPerCycle)
            {
                break;
            }

            // Apply the modification based on type
            // This is a placeholder - real implementation would have specific logic
            modifiedNodes.Add(modification.NodeId);
            appliedCount++;
        }

        await Task.CompletedTask;
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

            var node = nodeOption.Value;

            // Re-generate embedding
            var semanticText = $"{node.TypeName}: {node.PayloadJson}";
            var newEmbedding = await _embeddingModel.CreateEmbeddingsAsync(semanticText, ct);

            // Update cache
            _embeddingCache[nodeId] = newEmbedding;

            // Persist to Qdrant
            await _store.SaveNodeAsync(node, ct);
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

    private sealed record GraphModification(Guid NodeId, string ModificationType);
}
