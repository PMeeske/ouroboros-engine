namespace Ouroboros.Providers;

/// <summary>
/// Configuration for goal decomposition behavior.
/// Integrates with existing Pipeline.Planning.GoalDecomposer for hierarchical goals.
/// </summary>
public sealed record DecompositionConfig
{
    /// <summary>Maximum number of sub-goals to create.</summary>
    public int MaxSubGoals { get; init; } = 10;

    /// <summary>Whether to parallelize independent sub-goals.</summary>
    public bool ParallelizeIndependent { get; init; } = true;

    /// <summary>Prefer local models for simple tasks.</summary>
    public bool PreferLocalForSimple { get; init; } = true;

    /// <summary>Always use premium for final synthesis.</summary>
    public bool PremiumForSynthesis { get; init; } = true;

    /// <summary>Minimum complexity to warrant decomposition.</summary>
    public SubGoalComplexity DecompositionThreshold { get; init; } = SubGoalComplexity.Moderate;

    /// <summary>
    /// Use existing Pipeline.Planning.GoalDecomposer instead of inline decomposition.
    /// Requires a PipelineBranch to be provided for full integration.
    /// </summary>
    public bool UsePipelineGoalDecomposer { get; init; } = false;

    /// <summary>Custom routing rules by goal type.</summary>
    public Dictionary<SubGoalType, PathwayTier> TypeRouting { get; init; } = new()
    {
        [SubGoalType.Retrieval] = PathwayTier.Local,
        [SubGoalType.Transform] = PathwayTier.Local,
        [SubGoalType.Reasoning] = PathwayTier.CloudLight,
        [SubGoalType.Creative] = PathwayTier.CloudPremium,
        [SubGoalType.Coding] = PathwayTier.Specialized,
        [SubGoalType.Math] = PathwayTier.Specialized,
        [SubGoalType.Synthesis] = PathwayTier.CloudPremium
    };

    public static DecompositionConfig Default { get; } = new();

    public static DecompositionConfig LocalFirst { get; } = new()
    {
        PreferLocalForSimple = true,
        PremiumForSynthesis = false,
        TypeRouting = new()
        {
            [SubGoalType.Retrieval] = PathwayTier.Local,
            [SubGoalType.Transform] = PathwayTier.Local,
            [SubGoalType.Reasoning] = PathwayTier.Local,
            [SubGoalType.Creative] = PathwayTier.CloudLight,
            [SubGoalType.Coding] = PathwayTier.Local,
            [SubGoalType.Math] = PathwayTier.Local,
            [SubGoalType.Synthesis] = PathwayTier.CloudLight
        }
    };

    public static DecompositionConfig QualityFirst { get; } = new()
    {
        PreferLocalForSimple = false,
        PremiumForSynthesis = true,
        TypeRouting = new()
        {
            [SubGoalType.Retrieval] = PathwayTier.CloudLight,
            [SubGoalType.Transform] = PathwayTier.CloudLight,
            [SubGoalType.Reasoning] = PathwayTier.CloudPremium,
            [SubGoalType.Creative] = PathwayTier.CloudPremium,
            [SubGoalType.Coding] = PathwayTier.CloudPremium,
            [SubGoalType.Math] = PathwayTier.CloudPremium,
            [SubGoalType.Synthesis] = PathwayTier.CloudPremium
        }
    };

    /// <summary>
    /// Configuration that uses the existing Pipeline GoalDecomposer.
    /// Best for integration with existing goal hierarchies.
    /// </summary>
    public static DecompositionConfig PipelineIntegrated { get; } = new()
    {
        UsePipelineGoalDecomposer = true,
        PreferLocalForSimple = true,
        PremiumForSynthesis = true
    };
}