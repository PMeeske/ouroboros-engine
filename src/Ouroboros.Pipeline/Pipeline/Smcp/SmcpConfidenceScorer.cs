// <copyright file="SmcpConfidenceScorer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Numerics.Tensors;
using Ouroboros.Core.Hyperon;
using Ouroboros.Tools.MeTTa.Smcp;

namespace Ouroboros.Pipeline.Smcp;

/// <summary>
/// Computes composite confidence scores for SMCP tool activations.
/// <para>
/// <c>CompositeConfidence = IntentConfidence × MatchConfidence × ToolReliability</c>
/// </para>
/// <para>
/// When an <see cref="Ouroboros.Domain.IEmbeddingModel"/> is provided, match confidence
/// uses tensor cosine similarity (SIMD-accelerated via <see cref="TensorPrimitives"/>)
/// instead of keyword overlap. This makes Iaret <b>tensor-centric</b> — action routing
/// is driven by embedding similarity rather than LLM tool-call parsing.
/// </para>
/// </summary>
public sealed class SmcpConfidenceScorer
{
    private readonly ConcurrentDictionary<string, double> _toolReliability = new();
    private readonly ConcurrentDictionary<string, float[]> _toolEmbeddingCache = new();

#pragma warning disable CS0618 // IEmbeddingModel is obsolete but is the available interface
    private readonly Ouroboros.Domain.IEmbeddingModel? _embeddingModel;
#pragma warning restore CS0618

    private const double DefaultReliability = 0.8;
    private const double ReliabilityAlpha = 0.1; // EMA smoothing factor

    /// <summary>Creates a keyword-only scorer (legacy mode).</summary>
    public SmcpConfidenceScorer() { }

    /// <summary>
    /// Creates a tensor-centric scorer backed by an embedding model.
    /// Tool descriptions are embedded on first match and cached for SIMD cosine similarity.
    /// </summary>
#pragma warning disable CS0618
    public SmcpConfidenceScorer(Ouroboros.Domain.IEmbeddingModel embeddingModel)
    {
        _embeddingModel = embeddingModel;
    }
#pragma warning restore CS0618

    /// <summary>Whether this scorer uses tensor-based matching (true) or keyword-only (false).</summary>
    public bool IsTensorCentric => _embeddingModel != null;

    /// <summary>
    /// Computes the composite confidence for a tool match.
    /// </summary>
    public double Score(Expression intentAtom, SmcpToolAdapter adapter, Substitution bindings)
    {
        double intentConf = ExtractIntentConfidence(intentAtom);
        double matchConf = ComputeMatchConfidence(intentAtom, adapter);
        double reliability = GetReliability(adapter.Tool.Name);

        return intentConf * matchConf * reliability;
    }

    /// <summary>
    /// Records a tool execution outcome to update reliability tracking.
    /// </summary>
    /// <param name="toolName">The tool that was executed.</param>
    /// <param name="succeeded">Whether the execution succeeded.</param>
    public void RecordOutcome(string toolName, bool succeeded)
    {
        double current = GetReliability(toolName);
        double observation = succeeded ? 1.0 : 0.0;
        double updated = (ReliabilityAlpha * observation) + ((1.0 - ReliabilityAlpha) * current);
        _toolReliability[toolName] = updated;
    }

    /// <summary>
    /// Gets the current reliability score for a tool.
    /// </summary>
    public double GetReliability(string toolName) =>
        _toolReliability.GetValueOrDefault(toolName, DefaultReliability);

    /// <summary>
    /// Extracts the confidence value from the last child of an MkIntent atom.
    /// <c>(MkIntent verb args confidence)</c> → confidence as double.
    /// </summary>
    internal static double ExtractIntentConfidence(Expression intentAtom)
    {
        if (intentAtom.Children.Count >= 4 &&
            double.TryParse(intentAtom.Children[^1].ToSExpr(), out double conf))
        {
            return Math.Clamp(conf, 0.0, 1.0);
        }

        return 0.0;
    }

    /// <summary>
    /// Computes match confidence. When tensor-centric (embedding model available),
    /// uses SIMD cosine similarity between intent and tool embeddings.
    /// Falls back to keyword overlap when no embedding model is configured.
    /// </summary>
    internal double ComputeMatchConfidence(Expression intentAtom, SmcpToolAdapter adapter)
    {
        if (intentAtom.Children.Count < 3)
            return 0.0;

        // Tensor-centric path: cosine similarity between intent and tool embeddings
        if (_embeddingModel != null)
        {
            return ComputeTensorMatchConfidence(intentAtom, adapter);
        }

        // Keyword fallback path
        return ComputeKeywordMatchConfidence(intentAtom, adapter);
    }

    /// <summary>
    /// Tensor-centric match: embeds intent text and tool description, then computes
    /// cosine similarity using <see cref="TensorPrimitives"/> (SIMD-accelerated).
    /// Tool embeddings are cached for O(1) subsequent lookups.
    /// </summary>
    private double ComputeTensorMatchConfidence(Expression intentAtom, SmcpToolAdapter adapter)
    {
        try
        {
            // Build intent text from atom children
            var argsAtom = intentAtom.Children[2];
            var intentText = argsAtom.ToSExpr().Trim('"');
            if (intentAtom.Children.Count >= 2)
            {
                var verb = intentAtom.Children[1].ToSExpr().Trim('"');
                intentText = $"{verb} {intentText}";
            }

            // Get or compute tool embedding (cached)
            var toolEmb = _toolEmbeddingCache.GetOrAdd(
                adapter.Tool.Name,
                _ => _embeddingModel!.CreateEmbeddingsAsync(
                    $"{adapter.Tool.Name}: {adapter.Tool.Description}").GetAwaiter().GetResult());

            // Compute intent embedding (not cached — each intent is unique)
            var intentEmb = _embeddingModel!.CreateEmbeddingsAsync(intentText).GetAwaiter().GetResult();

            // Cosine similarity via TensorPrimitives (SIMD-accelerated on .NET 10)
            int len = Math.Min(intentEmb.Length, toolEmb.Length);
            if (len == 0) return 0.0;

            float similarity = TensorPrimitives.CosineSimilarity(
                intentEmb.AsSpan()[..len],
                toolEmb.AsSpan()[..len]);

            // Clamp to [0, 1] — cosine similarity can be negative for dissimilar vectors
            return Math.Clamp(similarity, 0.0, 1.0);
        }
        catch (OperationCanceledException) { throw; }
#pragma warning disable CA1031 // Embedding models can throw diverse exceptions
        catch (Exception)
#pragma warning restore CA1031
        {
            // Embedding failure — fall back to keyword matching
            return ComputeKeywordMatchConfidence(intentAtom, adapter);
        }
    }

    /// <summary>
    /// Keyword-based match confidence (legacy fallback).
    /// Full keyword overlap = 1.0, partial overlap degrades linearly.
    /// </summary>
    private static double ComputeKeywordMatchConfidence(Expression intentAtom, SmcpToolAdapter adapter)
    {
        var argsAtom = intentAtom.Children[2];
        var intentWords = ExtractWords(argsAtom);
        var toolKeywords = adapter.ActivationPattern.Keywords;

        if (toolKeywords.Count == 0)
            return 0.5; // No keywords to match against — neutral confidence

        int matches = toolKeywords.Count(kw =>
            intentWords.Any(w => w.Contains(kw, StringComparison.OrdinalIgnoreCase)));

        if (matches == 0) return 0.0;
        return 0.7 + (0.3 * Math.Min(1.0, (double)matches / toolKeywords.Count));
    }

    /// <summary>
    /// Pre-warms the tool embedding cache for all registered tool adapters.
    /// Call at startup for O(1) match confidence during runtime.
    /// </summary>
    public async Task PrecomputeToolEmbeddingsAsync(
        IEnumerable<SmcpToolAdapter> adapters, CancellationToken ct = default)
    {
        if (_embeddingModel == null) return;

        foreach (var adapter in adapters)
        {
            if (ct.IsCancellationRequested) break;
            if (_toolEmbeddingCache.ContainsKey(adapter.Tool.Name)) continue;

            var text = $"{adapter.Tool.Name}: {adapter.Tool.Description}";
            var embedding = await _embeddingModel.CreateEmbeddingsAsync(text, ct).ConfigureAwait(false);
            _toolEmbeddingCache[adapter.Tool.Name] = embedding;
        }
    }

    private static IReadOnlyList<string> ExtractWords(Atom atom)
    {
        if (atom is Expression expr)
            return expr.Children.Select(c => c.ToSExpr().Trim('"')).ToList();
        return [atom.ToSExpr().Trim('"')];
    }
}
