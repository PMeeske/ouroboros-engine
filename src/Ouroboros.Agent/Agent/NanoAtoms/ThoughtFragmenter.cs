// <copyright file="ThoughtFragmenter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Core.Monads;
using Ouroboros.Providers;

namespace Ouroboros.Agent.NanoAtoms;

/// <summary>
/// Splits input prompts into ThoughtFragments for NanoAtom processing.
/// Integrates with GoalDecomposer for intelligent decomposition when available,
/// falling back to naive token-based chunking.
/// </summary>
public sealed class ThoughtFragmenter
{
    private readonly Ouroboros.Abstractions.Core.IChatCompletionModel? _decompositionModel;
    private readonly NanoAtomConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThoughtFragmenter"/> class.
    /// </summary>
    /// <param name="config">Configuration for token budgets.</param>
    /// <param name="decompositionModel">Optional LLM for GoalDecomposer-based splitting.</param>
    public ThoughtFragmenter(
        NanoAtomConfig config,
        Ouroboros.Abstractions.Core.IChatCompletionModel? decompositionModel = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _decompositionModel = decompositionModel;
    }

    /// <summary>
    /// Fragments a prompt into ThoughtFragments that fit within the nano-context budget.
    /// Uses GoalDecomposer when available and configured, otherwise naive chunking.
    /// </summary>
    /// <param name="prompt">The prompt to fragment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Array of ThoughtFragments ready for NanoAtom processing.</returns>
    public async Task<ThoughtFragment[]> FragmentAsync(string prompt, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        int estimatedTokens = ThoughtFragment.EstimateTokenCount(prompt);

        // If the prompt fits in a single atom, no need to fragment
        if (estimatedTokens <= _config.MaxInputTokens)
        {
            return [ThoughtFragment.FromText(prompt)];
        }

        // Try GoalDecomposer-based splitting if configured and model available
        if (_config.UseGoalDecomposer && _decompositionModel != null)
        {
            var goalFragments = await TryGoalDecomposerAsync(prompt, ct);
            if (goalFragments != null && goalFragments.Length > 0)
            {
                return goalFragments;
            }
        }

        // Fallback: naive token-based chunking at sentence boundaries
        return NaiveChunk(prompt);
    }

    /// <summary>
    /// Attempts to use the LLM to decompose the prompt into semantic sub-goals,
    /// then converts each sub-goal to a ThoughtFragment.
    /// </summary>
    private async Task<ThoughtFragment[]?> TryGoalDecomposerAsync(string prompt, CancellationToken ct)
    {
        try
        {
            string decompositionPrompt =
                $"Decompose this into 2-4 focused sub-tasks. Return ONLY a JSON array of strings:\n\n{prompt}";

            string response = await _decompositionModel!.GenerateTextAsync(decompositionPrompt, ct);

            // Parse JSON array from response
            string[] subGoalTexts = ParseJsonArray(response);
            if (subGoalTexts.Length == 0)
            {
                return null;
            }

            return subGoalTexts
                .Select((text, i) => ThoughtFragment.FromSubGoal(SubGoal.FromDescription(text, i)))
                .ToArray();
        }
        catch
        {
            // GoalDecomposer failed — caller will fall back to naive chunking
            return null;
        }
    }

    /// <summary>
    /// Splits text into chunks at sentence boundaries, each fitting within the token budget.
    /// </summary>
    private ThoughtFragment[] NaiveChunk(string prompt)
    {
        int maxCharsPerChunk = _config.MaxInputTokens * 4; // ~4 chars per token
        List<ThoughtFragment> fragments = [];

        string[] sentences = prompt.Split(
            [". ", "! ", "? ", ".\n", "!\n", "?\n"],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        string currentChunk = string.Empty;
        int chunkIndex = 0;

        foreach (string sentence in sentences)
        {
            string candidate = string.IsNullOrEmpty(currentChunk)
                ? sentence
                : $"{currentChunk}. {sentence}";

            if (candidate.Length > maxCharsPerChunk && !string.IsNullOrEmpty(currentChunk))
            {
                // Current chunk is full, emit it
                fragments.Add(ThoughtFragment.FromText(currentChunk, chunkIndex++));
                currentChunk = sentence;
            }
            else
            {
                currentChunk = candidate;
            }
        }

        // Emit the last chunk
        if (!string.IsNullOrWhiteSpace(currentChunk))
        {
            fragments.Add(ThoughtFragment.FromText(currentChunk, chunkIndex));
        }

        // If no sentence boundaries found, split by character count
        if (fragments.Count == 0)
        {
            for (int i = 0; i < prompt.Length; i += maxCharsPerChunk)
            {
                int length = Math.Min(maxCharsPerChunk, prompt.Length - i);
                fragments.Add(ThoughtFragment.FromText(prompt.Substring(i, length), fragments.Count));
            }
        }

        return fragments.ToArray();
    }

    /// <summary>
    /// Parses a JSON string array from LLM output, handling common formatting variations.
    /// </summary>
    private static string[] ParseJsonArray(string response)
    {
        string trimmed = response.Trim();

        // Strip markdown code blocks
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            string[] lines = trimmed.Split('\n');
            trimmed = string.Join('\n',
                lines.Skip(1).TakeWhile(l => !l.StartsWith("```", StringComparison.Ordinal)));
        }

        // Find JSON array
        int start = trimmed.IndexOf('[');
        int end = trimmed.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            return [];
        }

        try
        {
            string json = trimmed[start..(end + 1)];
            string[]? result = System.Text.Json.JsonSerializer.Deserialize<string[]>(json);
            return result?.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray() ?? [];
        }
        catch
        {
            return [];
        }
    }
}
