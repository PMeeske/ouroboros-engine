// <copyright file="ThoughtConsolidator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.NanoAtoms;

/// <summary>
/// Merges DigestFragments from multiple ThoughtStreams into a unified ConsolidatedAction.
/// When all streams complete, the consolidator synthesizes the digests using either
/// an LLM call (if the combined digests fit in the token budget) or rule-based merging.
/// </summary>
public sealed class ThoughtConsolidator
{
    private readonly Ouroboros.Abstractions.Core.IChatCompletionModel? _synthesisModel;
    private readonly NanoAtomConfig _config;
    private readonly List<DigestFragment> _collectedDigests = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ThoughtConsolidator"/> class.
    /// </summary>
    /// <param name="config">Configuration for consolidation behavior.</param>
    /// <param name="synthesisModel">Optional LLM for synthesis. If null, uses rule-based merge.</param>
    public ThoughtConsolidator(
        NanoAtomConfig config,
        Ouroboros.Abstractions.Core.IChatCompletionModel? synthesisModel = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _synthesisModel = synthesisModel;
    }

    /// <summary>Gets the number of digests collected so far.</summary>
    public int DigestCount => _collectedDigests.Count;

    /// <summary>
    /// Adds a digest fragment to the collection.
    /// </summary>
    /// <param name="digest">The digest to add.</param>
    public void AddDigest(DigestFragment digest)
    {
        ArgumentNullException.ThrowIfNull(digest);
        _collectedDigests.Add(digest);
    }

    /// <summary>
    /// Adds multiple digest fragments to the collection.
    /// </summary>
    /// <param name="digests">The digests to add.</param>
    public void AddDigests(IEnumerable<DigestFragment> digests)
    {
        ArgumentNullException.ThrowIfNull(digests);
        _collectedDigests.AddRange(digests);
    }

    /// <summary>
    /// Consolidates all collected digests into a single ConsolidatedAction.
    /// Uses LLM synthesis if a model is available and the combined content fits;
    /// otherwise falls back to rule-based merging.
    /// </summary>
    /// <param name="streamCount">Number of streams that contributed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the ConsolidatedAction or an error.</returns>
    public async Task<Result<ConsolidatedAction, string>> ConsolidateAsync(
        int streamCount,
        CancellationToken ct = default)
    {
        if (_collectedDigests.Count == 0)
        {
            return Result<ConsolidatedAction, string>.Failure("No digests to consolidate");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Calculate aggregate confidence
            double aggregateConfidence = _collectedDigests.Average(d => d.Confidence);

            // Check if confidence meets threshold
            if (aggregateConfidence < _config.ConsolidationThreshold)
            {
                // Below threshold but still produce output with low confidence marker
                aggregateConfidence = Math.Min(aggregateConfidence, _config.ConsolidationThreshold - 0.01);
            }

            // Synthesize the final content
            string content;
            string actionType;

            if (_synthesisModel != null)
            {
                (content, actionType) = await TrySynthesizeAsync(ct).ConfigureAwait(false);
            }
            else
            {
                (content, actionType) = RuleBasedMerge();
            }

            stopwatch.Stop();

            return Result<ConsolidatedAction, string>.Success(new ConsolidatedAction(
                Id: Guid.NewGuid(),
                Content: content,
                SourceDigests: _collectedDigests.AsReadOnly(),
                Confidence: aggregateConfidence,
                ActionType: actionType,
                StreamCount: streamCount,
                ElapsedMs: stopwatch.ElapsedMilliseconds,
                Timestamp: DateTime.UtcNow));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<ConsolidatedAction, string>.Failure($"Consolidation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Attempts to synthesize digests using the LLM. Falls back to rule-based if LLM fails.
    /// </summary>
    private async Task<(string Content, string ActionType)> TrySynthesizeAsync(CancellationToken ct)
    {
        try
        {
            string combinedDigests = string.Join(
                "\n---\n",
                _collectedDigests.Select((d, i) => $"[Thought {i + 1}] {d.Content}"));

            // Check if combined digests fit in token budget (they should — they're already compressed)
            int estimatedTokens = ThoughtFragment.EstimateTokenCount(combinedDigests);
            if (estimatedTokens > _config.MaxInputTokens * 2)
            {
                // Too large even after compression — fall back to rule-based
                return RuleBasedMerge();
            }

            string synthesisPrompt =
                $"Synthesize these thought fragments into a single coherent response:\n\n{combinedDigests}";

            string content = await _synthesisModel!.GenerateTextAsync(synthesisPrompt, ct).ConfigureAwait(false);
            return (content, InferActionType(content));
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return RuleBasedMerge();
        }
    }

    /// <summary>
    /// Rule-based merge fallback — concatenates and deduplicates digest contents.
    /// </summary>
    private (string Content, string ActionType) RuleBasedMerge()
    {
        // Order by confidence (highest first) and concatenate
        var ordered = _collectedDigests
            .OrderByDescending(d => d.Confidence)
            .Select(d => d.Content.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string content = string.Join("\n\n", ordered);
        return (content, InferActionType(content));
    }

    /// <summary>
    /// Infers the action type from content using simple keyword heuristics.
    /// </summary>
    private static string InferActionType(string content)
    {
        string lower = content.ToLowerInvariant();

        if (lower.Contains("```") || lower.Contains("function") || lower.Contains("class "))
        {
            return "code";
        }

        if (lower.Contains("step 1") || lower.Contains("plan:") || lower.Contains("first,"))
        {
            return "plan";
        }

        if (lower.Contains("issue") || lower.Contains("problem") || lower.Contains("improve"))
        {
            return "critique";
        }

        return "response";
    }
}
