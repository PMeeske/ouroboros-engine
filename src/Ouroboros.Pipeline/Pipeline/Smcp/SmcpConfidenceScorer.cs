// <copyright file="SmcpConfidenceScorer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using Ouroboros.Core.Hyperon;
using Ouroboros.Tools.MeTTa.Smcp;

namespace Ouroboros.Pipeline.Smcp;

/// <summary>
/// Computes composite confidence scores for SMCP tool activations.
/// <para>
/// <c>CompositeConfidence = IntentConfidence × MatchConfidence × ToolReliability</c>
/// </para>
/// <list type="bullet">
/// <item><term>IntentConfidence</term><description>How confident the classifier is about the intent (from the MkIntent atom).</description></item>
/// <item><term>MatchConfidence</term><description>How well the tool's activation pattern matched (1.0 for exact unification, degraded for partial keyword overlap).</description></item>
/// <item><term>ToolReliability</term><description>Historical success rate of the tool (exponential moving average).</description></item>
/// </list>
/// </summary>
public sealed class SmcpConfidenceScorer
{
    private readonly ConcurrentDictionary<string, double> _toolReliability = new();
    private const double DefaultReliability = 0.8;
    private const double ReliabilityAlpha = 0.1; // EMA smoothing factor

    /// <summary>
    /// Computes the composite confidence for a tool match.
    /// </summary>
    /// <param name="intentAtom">The <c>MkIntent</c> atom (confidence is the last child).</param>
    /// <param name="adapter">The tool's SMCP adapter (contains keywords for match scoring).</param>
    /// <param name="bindings">Unification bindings from pattern matching.</param>
    /// <returns>Composite confidence score between 0.0 and 1.0.</returns>
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
    /// Computes match confidence based on keyword overlap between intent args and tool keywords.
    /// Full overlap = 1.0, partial overlap degrades linearly.
    /// </summary>
    internal static double ComputeMatchConfidence(Expression intentAtom, SmcpToolAdapter adapter)
    {
        if (intentAtom.Children.Count < 3)
            return 0.0;

        var argsAtom = intentAtom.Children[2];
        var intentWords = ExtractWords(argsAtom);
        var toolKeywords = adapter.ActivationPattern.Keywords;

        if (toolKeywords.Count == 0)
            return 0.5; // No keywords to match against — neutral confidence

        int matches = toolKeywords.Count(kw =>
            intentWords.Any(w => w.Contains(kw, StringComparison.OrdinalIgnoreCase)));

        // Score: at least 1 keyword match gives 0.7 base, more matches scale up to 1.0
        if (matches == 0) return 0.0;
        return 0.7 + (0.3 * Math.Min(1.0, (double)matches / toolKeywords.Count));
    }

    private static IReadOnlyList<string> ExtractWords(Atom atom)
    {
        if (atom is Expression expr)
            return expr.Children.Select(c => c.ToSExpr().Trim('"')).ToList();
        return new[] { atom.ToSExpr().Trim('"') };
    }
}
