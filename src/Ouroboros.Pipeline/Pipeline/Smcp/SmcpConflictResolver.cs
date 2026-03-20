// <copyright file="SmcpConflictResolver.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Tools.MeTTa.Smcp;

namespace Ouroboros.Pipeline.Smcp;

/// <summary>
/// Resolves conflicts when multiple tools match the same intent.
/// Strategies: highest confidence wins, or parallel execution for non-overlapping capabilities.
/// </summary>
public sealed class SmcpConflictResolver
{
    /// <summary>
    /// Resolves a set of tool matches to determine which tools should fire.
    /// </summary>
    /// <param name="matches">All tools that matched the intent above threshold.</param>
    /// <returns>The matches that should actually fire (one for exclusive, multiple for parallel).</returns>
    public IReadOnlyList<SmcpToolMatch> Resolve(IReadOnlyList<SmcpToolMatch> matches)
    {
        if (matches.Count <= 1)
            return matches;

        // Check if tools have non-overlapping keywords (can run in parallel)
        if (AreNonOverlapping(matches))
            return matches;

        // Otherwise, highest confidence wins
        var best = matches.MaxBy(m => m.CompositeConfidence);
        return best is not null ? new[] { best } : Array.Empty<SmcpToolMatch>();
    }

    /// <summary>
    /// Checks if the matched tools have non-overlapping keyword sets,
    /// meaning they handle different aspects of the intent and can run in parallel.
    /// </summary>
    private static bool AreNonOverlapping(IReadOnlyList<SmcpToolMatch> matches)
    {
        for (int i = 0; i < matches.Count; i++)
        {
            for (int j = i + 1; j < matches.Count; j++)
            {
                if (matches[i].ToolName == matches[j].ToolName)
                    return false; // Same tool matched twice — not parallel
            }
        }

        // All different tools — allow parallel execution
        return true;
    }
}
