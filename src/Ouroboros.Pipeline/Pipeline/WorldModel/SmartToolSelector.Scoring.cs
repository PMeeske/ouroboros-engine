// <copyright file="SmartToolSelector.Scoring.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.WorldModel;

using Ouroboros.Pipeline.Planning;
using Ouroboros.Tools;

/// <summary>
/// Partial class containing scoring, constraint filtering, and reasoning helpers.
/// </summary>
public sealed partial class SmartToolSelector
{
    private static double EstimateToolCost(ITool tool, WorldState state)
    {
        // Check if cost information is available in world state
        if (state.Observations.TryGetValue($"tool.{tool.Name}.cost", out Observation? costObs)
            && costObs.Value is double cost)
        {
            return Math.Clamp(cost, 0.0, 1.0);
        }

        // Default cost estimation based on tool complexity (schema presence indicates complexity)
        return tool.JsonSchema != null ? 0.6 : 0.3;
    }

    private static double EstimateToolSpeed(ITool tool, WorldState state)
    {
        // Check if speed information is available in world state
        if (state.Observations.TryGetValue($"tool.{tool.Name}.speed", out Observation? speedObs)
            && speedObs.Value is double speed)
        {
            return Math.Clamp(speed, 0.0, 1.0);
        }

        // Default speed estimation
        return 0.5;
    }

    private static double EstimateToolQuality(ITool tool, double fitScore)
    {
        // Quality is primarily derived from fit score with some base quality
        double baseQuality = 0.3;
        return Math.Clamp(baseQuality + (fitScore * 0.7), 0.0, 1.0);
    }

    private static string BuildReasoning(
        Goal goal,
        IReadOnlyList<ToolCandidate> selectedCandidates,
        IReadOnlyList<Constraint> constraints)
    {
        if (selectedCandidates.Count == 0)
        {
            return $"No suitable tools found for goal: {goal.Description}";
        }

        List<string> reasoningParts = new()
        {
            $"Selected {selectedCandidates.Count} tool(s) for goal: \"{goal.Description}\".",
        };

        foreach (ToolCandidate candidate in selectedCandidates)
        {
            string capabilities = candidate.MatchedCapabilities.Count > 0
                ? $" (matched: {string.Join(", ", candidate.MatchedCapabilities)})"
                : string.Empty;

            reasoningParts.Add($"- {candidate.Tool.Name}: fit={candidate.FitScore:F2}{capabilities}");
        }

        if (constraints.Count > 0)
        {
            reasoningParts.Add($"Applied {constraints.Count} constraint(s): {string.Join(", ", constraints.Select(c => c.Name))}.");
        }

        return string.Join(" ", reasoningParts);
    }

    private static List<ToolCandidate> ApplySingleConstraint(
        List<ToolCandidate> candidates,
        Constraint constraint)
    {
        // Parse constraint rules and filter candidates
        // Supported constraint formats:
        // - "exclude:tool_name" - excludes a specific tool
        // - "require:capability" - requires a specific capability
        // - "max_cost:0.5" - maximum cost threshold
        // - "min_quality:0.7" - minimum quality threshold

        string rule = constraint.Rule.Trim().ToLowerInvariant();

        if (rule.StartsWith("exclude:", StringComparison.Ordinal))
        {
            string toolName = rule.Substring("exclude:".Length).Trim();
            return candidates
                .Where(c => !c.Tool.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (rule.StartsWith("require:", StringComparison.Ordinal))
        {
            string capability = rule.Substring("require:".Length).Trim();
            return candidates
                .Where(c => c.MatchedCapabilities.Any(cap =>
                    cap.Contains(capability, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        if (rule.StartsWith("max_cost:", StringComparison.Ordinal))
        {
            string costStr = rule.Substring("max_cost:".Length).Trim();
            if (double.TryParse(costStr, out double maxCost))
            {
                return candidates
                    .Where(c => c.CostScore <= maxCost)
                    .ToList();
            }
        }

        if (rule.StartsWith("min_quality:", StringComparison.Ordinal))
        {
            string qualityStr = rule.Substring("min_quality:".Length).Trim();
            if (double.TryParse(qualityStr, out double minQuality))
            {
                return candidates
                    .Where(c => c.QualityScore >= minQuality)
                    .ToList();
            }
        }

        // Unknown constraint format - return candidates unchanged
        return candidates;
    }

    private static bool IsToolBlockedByConstraint(ITool tool, Constraint constraint)
    {
        string rule = constraint.Rule.Trim().ToLowerInvariant();

        if (rule.StartsWith("exclude:", StringComparison.Ordinal))
        {
            string toolName = rule.Substring("exclude:".Length).Trim();
            return tool.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
