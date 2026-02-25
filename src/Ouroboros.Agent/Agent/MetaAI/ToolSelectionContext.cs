namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Context for fine-tuned tool selection.
/// </summary>
public sealed record ToolSelectionContext
{
    /// <summary>Maximum number of tools to select.</summary>
    public int? MaxTools { get; init; }

    /// <summary>Required tool categories.</summary>
    public List<ToolCategory>? RequiredCategories { get; init; }

    /// <summary>Excluded tool categories.</summary>
    public List<ToolCategory>? ExcludedCategories { get; init; }

    /// <summary>Required tool names.</summary>
    public List<string>? RequiredToolNames { get; init; }

    /// <summary>Whether to prioritize fast tools.</summary>
    public bool PreferFastTools { get; init; }

    /// <summary>Whether to prioritize reliable tools.</summary>
    public bool PreferReliableTools { get; init; }
}