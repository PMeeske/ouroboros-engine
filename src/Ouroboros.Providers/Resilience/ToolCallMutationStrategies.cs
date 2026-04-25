// <copyright file="ToolCallMutationStrategies.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.Resilience;

/// <summary>
/// Adds format hints to the prompt instructing the LLM to use a specific
/// tool call output format.
/// </summary>
/// <remarks>
/// Priority 10 — tried first because it's the least invasive mutation.
/// Many tool-call failures stem from the model not knowing which format to use.
/// </remarks>
public sealed class FormatHintMutation : IMutationStrategy<ToolCallContext>
{
    /// <inheritdoc/>
    public string Name => "format-hint";

    /// <inheritdoc/>
    public int Priority => 10;

    /// <inheritdoc/>
    public bool CanMutate(ToolCallContext context, Exception lastError)
    {
        // Applicable when no format hint has been added yet
        return !context.Prompt.Contains("tool_call>", StringComparison.OrdinalIgnoreCase)
            && !context.Prompt.Contains("tool_calls", StringComparison.OrdinalIgnoreCase)
            && !context.Prompt.Contains("[TOOL:", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public ToolCallContext Mutate(ToolCallContext context, int generation)
    {
        string formatInstruction = context.PreferredFormat switch
        {
            ToolCallFormat.XmlTag =>
                "\n\nWhen you need to call a tool, use this exact format:\n<tool_call>{\"name\":\"tool_name\",\"arguments\":{...}}</tool_call>",
            ToolCallFormat.JsonToolCalls =>
                "\n\nWhen you need to call a tool, respond with JSON:\n{\"tool_calls\":[{\"function\":{\"name\":\"tool_name\",\"arguments\":{...}}}]}",
            ToolCallFormat.BracketLegacy =>
                "\n\nWhen you need to call a tool, use: [TOOL:tool_name arguments]",
            _ =>
                "\n\nWhen you need to call a tool, use this exact format:\n<tool_call>{\"name\":\"tool_name\",\"arguments\":{...}}</tool_call>",
        };

        context.Prompt += formatInstruction;
        context.Generation = generation;
        context.History.Add(new MutationHistoryEntry(Name, generation, Error: null!, DateTime.UtcNow));
        return context;
    }
}

/// <summary>
/// Switches the preferred tool call format to a different one.
/// </summary>
/// <remarks>
/// Priority 20 — tried after format hints. Some models respond better
/// to certain formats (e.g., Mistral prefers XML, GPT-compatible models prefer JSON).
/// </remarks>
public sealed class FormatSwitchMutation : IMutationStrategy<ToolCallContext>
{
    private static readonly ToolCallFormat[] Formats =
    [
        ToolCallFormat.XmlTag,
        ToolCallFormat.JsonToolCalls,
        ToolCallFormat.BracketLegacy
    ];

    /// <inheritdoc/>
    public string Name => "format-switch";

    /// <inheritdoc/>
    public int Priority => 20;

    /// <inheritdoc/>
    public bool CanMutate(ToolCallContext context, Exception lastError)
    {
        // Can switch if we haven't tried all formats yet
        int formatsTried = context.History
            .Count(h => h.StrategyName == Name);
        return formatsTried < Formats.Length - 1;
    }

    /// <inheritdoc/>
    public ToolCallContext Mutate(ToolCallContext context, int generation)
    {
        // Cycle to the next format
        int currentIndex = Array.IndexOf(Formats, context.PreferredFormat);
        int nextIndex = (currentIndex + 1) % Formats.Length;
        context.PreferredFormat = Formats[nextIndex];

        // Replace any existing format instruction in the prompt
        string[] formatMarkers = ["<tool_call>", "tool_calls", "[TOOL:"];
        string cleanPrompt = context.Prompt;
        foreach (string marker in formatMarkers)
        {
            int idx = cleanPrompt.IndexOf("\n\nWhen you need to call a tool", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                cleanPrompt = cleanPrompt[..idx];
            }
        }

        context.Prompt = cleanPrompt;
        context.Generation = generation;
        context.History.Add(new MutationHistoryEntry(Name, generation, Error: null!, DateTime.UtcNow));

        // Re-apply format hint with the new format by delegating to FormatHintMutation
        return new FormatHintMutation().Mutate(context, generation);
    }
}

/// <summary>
/// Reduces the number of tools exposed to the LLM to reduce confusion.
/// </summary>
/// <remarks>
/// Priority 30 — tried when format changes don't help. Some small models
/// get overwhelmed by too many tool definitions and produce garbage.
/// </remarks>
public sealed class ToolSimplificationMutation : IMutationStrategy<ToolCallContext>
{
    /// <inheritdoc/>
    public string Name => "tool-simplification";

    /// <inheritdoc/>
    public int Priority => 30;

    /// <inheritdoc/>
    public bool CanMutate(ToolCallContext context, Exception lastError)
    {
        // Can simplify if there are more than 3 tools
        return context.Tools.Count > 3;
    }

    /// <inheritdoc/>
    public ToolCallContext Mutate(ToolCallContext context, int generation)
    {
        // Keep only the top half of tools (assume first tools are most relevant)
        int keepCount = Math.Max(3, context.Tools.Count / 2);
        context.Tools = context.Tools.Take(keepCount).ToList();
        context.Generation = generation;
        context.History.Add(new MutationHistoryEntry(Name, generation, Error: null!, DateTime.UtcNow));
        return context;
    }
}

/// <summary>
/// Adjusts the sampling temperature to get more or less deterministic output.
/// </summary>
/// <remarks>
/// Priority 40 — tried as a last resort before exhausting retries.
/// Lower temperature may help the model produce more structured output;
/// higher temperature may help escape local optima.
/// </remarks>
public sealed class TemperatureMutation : IMutationStrategy<ToolCallContext>
{
    /// <inheritdoc/>
    public string Name => "temperature";

    /// <inheritdoc/>
    public int Priority => 40;

    /// <inheritdoc/>
    public bool CanMutate(ToolCallContext context, Exception lastError)
    {
        // Always applicable as a last resort
        return true;
    }

    /// <inheritdoc/>
    public ToolCallContext Mutate(ToolCallContext context, int generation)
    {
        // Alternate between lowering and raising temperature
        context.Temperature = generation % 2 == 0
            ? Math.Max(0.1f, context.Temperature * 0.5f) // More deterministic
            : Math.Min(1.5f, context.Temperature * 1.5f); // More creative

        context.Generation = generation;
        context.History.Add(new MutationHistoryEntry(Name, generation, Error: null!, DateTime.UtcNow));
        return context;
    }
}
