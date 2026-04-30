// <copyright file="MeTTaPromptCompressor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Providers;

/// <summary>
/// Symbolic prompt compressor backed by MeTTa reasoning.
/// When a prompt exceeds a byte threshold, decomposes it into atoms,
/// queries MeTTa for essential distinctions, and reconstructs a
/// condensed prompt preserving core intent.
/// </summary>
/// <remarks>
/// Falls back to sentence-level truncation if MeTTa is unavailable
/// or compression does not yield sufficient reduction.
/// </remarks>
public sealed class MeTTaPromptCompressor
{
    private readonly IMeTTaEngine? _engine;
    private readonly int _targetRatioPercent;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeTTaPromptCompressor"/> class.
    /// </summary>
    /// <param name="engine">Optional MeTTa engine for symbolic compression.</param>
    /// <param name="targetRatioPercent">Target size as percent of original (default 60%).</param>
    public MeTTaPromptCompressor(IMeTTaEngine? engine = null, int targetRatioPercent = 60)
    {
        _engine = engine;
        _targetRatioPercent = Math.Clamp(targetRatioPercent, 10, 90);
    }

    /// <summary>
    /// Compresses <paramref name="prompt"/> to fit within <paramref name="maxBytes"/>
    /// using MeTTa-guided symbolic distillation when possible.
    /// </summary>
    /// <param name="prompt">The original prompt text.</param>
    /// <param name="maxBytes">Maximum allowed UTF-8 byte length.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A compressed prompt string.</returns>
    public async Task<string> CompressAsync(string prompt, int maxBytes, CancellationToken ct = default)
    {
        int originalBytes = Encoding.UTF8.GetByteCount(prompt);
        if (originalBytes <= maxBytes)
            return prompt;

        // Phase 1: fast heuristic deduplication
        string deduped = HeuristicDedupe(prompt);
        int dedupedBytes = Encoding.UTF8.GetByteCount(deduped);
        if (dedupedBytes <= maxBytes)
            return deduped;

        // Phase 2: MeTTa symbolic compression (if engine available)
        if (_engine is not null)
        {
            string? mettaCompressed = await TryMeTTaCompressAsync(deduped, maxBytes, ct).ConfigureAwait(false);
            if (mettaCompressed is not null && Encoding.UTF8.GetByteCount(mettaCompressed) <= maxBytes)
                return mettaCompressed;
        }

        // Phase 3: sentence-level truncation preserving newest content
        return SentenceTruncate(deduped, maxBytes);
    }

    /// <summary>
    /// Runs a MeTTa query that decomposes the prompt into atoms,
    /// scores each atom by information density, and reconstructs
    /// from the highest-scoring subset.
    /// </summary>
    private async Task<string?> TryMeTTaCompressAsync(string prompt, int maxBytes, CancellationToken ct)
    {
        try
        {
            // Build a MeTTa space of sentences and keywords
            string facts = BuildMeTTaFacts(prompt);
            var addResult = await _engine!.AddFactAsync(facts, ct).ConfigureAwait(false);
            if (addResult.IsFailure)
                return null;

            // Query for the most informative atoms (distinctions with highest arity/depth)
            string query =
                "!(match &self (Distinction $text $score) (pair $text $score))";

            var result = await _engine.ExecuteQueryAsync(query, ct).ConfigureAwait(false);
            if (result.IsFailure)
                return null;

            string reconstructed = ReconstructFromMeTTaResult(result.Value, maxBytes);
            return string.IsNullOrWhiteSpace(reconstructed) ? null : reconstructed;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Breaks prompt into sentences and emits MeTTa facts:
    /// (Sentence $id $text), (Keyword $id $word),
    /// (Distinction $text $score) where score is keyword density.
    /// </summary>
    private static string BuildMeTTaFacts(string prompt)
    {
        var sb = new StringBuilder();
        string[] sentences = SplitIntoSentences(prompt);
        var keywordFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // First pass: count keyword frequencies across all sentences
        foreach (string sentence in sentences)
        {
            foreach (string kw in ExtractKeywords(sentence))
            {
                keywordFreq[kw] = keywordFreq.GetValueOrDefault(kw) + 1;
            }
        }

        // Second pass: emit facts with novelty scoring (rarer keywords = higher score)
        for (int i = 0; i < sentences.Length; i++)
        {
            string text = sentences[i].Replace("\"", "\\\"");
            sb.AppendLine($"(Sentence {i} \"{text}\")");

            string[] kws = ExtractKeywords(sentences[i]);
            double score = 0;
            foreach (string kw in kws)
            {
                int freq = keywordFreq.GetValueOrDefault(kw, 1);
                score += 1.0 / freq; // rarer keywords contribute more
            }

            if (kws.Length > 0)
            {
                score = Math.Round(score * 100 / kws.Length, 2);
                sb.AppendLine($"(Distinction \"{text}\" {score})");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Parses MeTTa query result (a list of (pair text score) atoms)
    /// and rebuilds a prompt from highest-scoring distinctions up to maxBytes.
    /// </summary>
    private string ReconstructFromMeTTaResult(string mettaResult, int maxBytes)
    {
        var distinctions = new List<(string Text, double Score)>();

        // Parse MeTTa output lines like: [(pair "sentence text" 42.5)]
        foreach (string line in mettaResult.Split('\n'))
        {
            Match m = Regex.Match(line, @"\(pair\s+""([^""]+)""\s+([\d.]+)\)");
            if (m.Success && double.TryParse(m.Groups[2].Value, out double score))
            {
                distinctions.Add((m.Groups[1].Value, score));
            }
        }

        // Sort by score descending and greedily pack into maxBytes
        distinctions = distinctions.OrderByDescending(d => d.Score).ToList();

        var sb = new StringBuilder();
        int bytesSoFar = 0;
        var seen = new HashSet<string>();

        foreach ((string text, _) in distinctions)
        {
            if (seen.Contains(text))
                continue;

            int sentenceBytes = Encoding.UTF8.GetByteCount(text + " ");
            if (bytesSoFar + sentenceBytes > maxBytes)
                break;

            if (sb.Length > 0)
                sb.Append(' ');
            sb.Append(text);
            bytesSoFar += sentenceBytes;
            seen.Add(text);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Fast heuristic deduplication: removes repeated whitespace and
    /// collapses near-identical consecutive sentences.
    /// </summary>
    private static string HeuristicDedupe(string prompt)
    {
        string cleaned = Regex.Replace(prompt, @"\s+", " ");
        string[] sentences = SplitIntoSentences(cleaned);
        var deduped = new List<string>();
        string? last = null;

        foreach (string s in sentences)
        {
            string normalized = s.ToLowerInvariant().TrimEnd('.', '!', '?');
            if (last is null || !normalized.Equals(last, StringComparison.Ordinal))
            {
                deduped.Add(s);
                last = normalized;
            }
        }

        return string.Join(' ', deduped);
    }

    /// <summary>
    /// Truncates by dropping oldest sentences while keeping the most recent ones,
    /// under the assumption that later context is more relevant.
    /// </summary>
    private static string SentenceTruncate(string prompt, int maxBytes)
    {
        string[] sentences = SplitIntoSentences(prompt);
        var sb = new StringBuilder();
        int bytesSoFar = 0;

        // Walk backwards from newest sentence
        for (int i = sentences.Length - 1; i >= 0; i--)
        {
            int sentenceBytes = Encoding.UTF8.GetByteCount(sentences[i] + (i < sentences.Length - 1 ? " " : ""));
            if (bytesSoFar + sentenceBytes > maxBytes)
                break;

            sb.Insert(0, sentences[i] + (sb.Length > 0 ? " " : ""));
            bytesSoFar += sentenceBytes;
        }

        return sb.ToString();
    }

    private static string[] SplitIntoSentences(string text)
    {
        // Split on sentence boundaries, preserving the delimiter
        string[] parts = Regex.Split(text, @"(?<=[.!?])\s+");
        return parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
    }

    private static string[] ExtractKeywords(string sentence)
    {
        // Simple keyword extraction: alphanumeric tokens longer than 3 chars,
        // excluding common stop words
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "for", "are", "but", "not", "you", "all", "can", "had", "her", "was", "one", "our", "out", "day", "get", "has", "him", "his", "how", "its", "may", "new", "now", "old", "see", "two", "way", "who", "boy", "did", "she", "use", "her", "now", "him", "than", "like", "time", "very", "when", "come", "here", "just", "like", "long", "make", "many", "over", "such", "take", "than", "them", "well", "were"
        };

        return Regex.Matches(sentence, @"\b[a-zA-Z]{4,}\b")
            .Select(m => m.Value)
            .Where(w => !stopWords.Contains(w))
            .ToArray();
    }
}
