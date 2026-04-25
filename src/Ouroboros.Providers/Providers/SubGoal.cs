namespace Ouroboros.Providers;

/// <summary>
/// A sub-goal decomposed from a larger request.
/// Compatible with Pipeline.Planning.Goal - use SubGoalExtensions in Ouroboros.Pipeline
/// to convert between types when integrating with GoalDecomposer/HierarchicalGoalPlanner.
/// </summary>
public sealed partial record SubGoal(
    string Id,
    string Description,
    SubGoalComplexity Complexity,
    SubGoalType Type,
    IReadOnlyList<string> Dependencies,
    PathwayTier PreferredTier)
{
    /// <summary>
    /// Creates a SubGoal with inferred routing metadata from a description.
    /// </summary>
    /// <returns></returns>
    public static SubGoal FromDescription(string description, int index = 0)
    {
        return new SubGoal(
            Id: $"goal_{index + 1}",
            Description: description,
            Complexity: InferComplexity(description),
            Type: InferGoalType(description),
            Dependencies: Array.Empty<string>(),
            PreferredTier: InferTier(description));
    }

    private static SubGoalComplexity InferComplexity(string text)
    {
        var length = text.Length;
        var hasMultipleSteps = MultipleStepsRegex().IsMatch(text);

        if (length < 50)
        {
            return SubGoalComplexity.Simple;
        }

        if (length < 200 && !hasMultipleSteps)
        {
            return SubGoalComplexity.Moderate;
        }

        if (length < 500)
        {
            return SubGoalComplexity.Complex;
        }

        return SubGoalComplexity.Expert;
    }

    private static SubGoalType InferGoalType(string text)
    {
        var lower = text.ToLowerInvariant();

        if (CodingKeywordsRegex().IsMatch(lower))
        {
            return SubGoalType.Coding;
        }

        if (MathKeywordsRegex().IsMatch(lower))
        {
            return SubGoalType.Math;
        }

        if (CreativeKeywordsRegex().IsMatch(lower))
        {
            return SubGoalType.Creative;
        }

        if (ReasoningKeywordsRegex().IsMatch(lower))
        {
            return SubGoalType.Reasoning;
        }

        if (TransformKeywordsRegex().IsMatch(lower))
        {
            return SubGoalType.Transform;
        }

        if (RetrievalKeywordsRegex().IsMatch(lower))
        {
            return SubGoalType.Retrieval;
        }

        return SubGoalType.Reasoning;
    }

    private static PathwayTier InferTier(string text)
    {
        var type = InferGoalType(text);
        var complexity = InferComplexity(text);

        if (complexity <= SubGoalComplexity.Simple)
        {
            return PathwayTier.Local;
        }

        return type switch
        {
            SubGoalType.Retrieval => PathwayTier.Local,
            SubGoalType.Transform => PathwayTier.Local,
            SubGoalType.Coding => PathwayTier.Specialized,
            SubGoalType.Math => PathwayTier.Specialized,
            SubGoalType.Creative => PathwayTier.CloudPremium,
            SubGoalType.Synthesis => PathwayTier.CloudPremium,
            _ => PathwayTier.CloudLight,
        };
    }

    [GeneratedRegex(@"\b(then|next|after|finally|also|and then)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MultipleStepsRegex();

    [GeneratedRegex(@"\b(code|program|function|class|implement|debug|refactor)\b")]
    private static partial Regex CodingKeywordsRegex();

    [GeneratedRegex(@"\b(calculate|compute|solve|equation|formula|math)\b")]
    private static partial Regex MathKeywordsRegex();

    [GeneratedRegex(@"\b(write|create|compose|generate|story|poem|creative)\b")]
    private static partial Regex CreativeKeywordsRegex();

    [GeneratedRegex(@"\b(analyze|compare|evaluate|reason|explain why)\b")]
    private static partial Regex ReasoningKeywordsRegex();

    [GeneratedRegex(@"\b(convert|transform|format|translate|summarize)\b")]
    private static partial Regex TransformKeywordsRegex();

    [GeneratedRegex(@"\b(find|search|lookup|what is|who is|when)\b")]
    private static partial Regex RetrievalKeywordsRegex();
}
