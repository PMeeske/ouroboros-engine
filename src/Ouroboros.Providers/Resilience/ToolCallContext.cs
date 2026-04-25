// <copyright file="ToolCallContext.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.Resilience;

/// <summary>
/// Mutable execution context for evolutionary tool call retries.
/// Each generation mutates this context to adapt the request.
/// </summary>
public sealed class ToolCallContext
{
    /// <summary>
    /// Gets or sets the prompt text to send to the LLM.
    /// Mutations may prepend format hints or rephrase.
    /// </summary>
    public required string Prompt { get; set; }

    /// <summary>
    /// Gets or sets the tool definitions available for this call.
    /// Mutations may simplify or reduce the set.
    /// </summary>
    public required IReadOnlyList<ToolDefinitionSlim> Tools { get; set; }

    /// <summary>
    /// Gets or sets the preferred tool call format.
    /// Mutations may switch between XML, JSON, and bracket formats.
    /// </summary>
    public ToolCallFormat PreferredFormat { get; set; } = ToolCallFormat.XmlTag;

    /// <summary>
    /// Gets or sets the sampling temperature.
    /// Mutations may adjust for more/less variability.
    /// </summary>
    public float Temperature { get; set; } = 0.7f;

    /// <summary>
    /// Gets or sets the current generation/iteration number (0-based).
    /// </summary>
    public int Generation { get; set; }

    /// <summary>
    /// Gets the history of mutations applied and their triggering errors.
    /// </summary>
    public List<MutationHistoryEntry> History { get; } = [];

    /// <summary>
    /// Creates a deep copy of this context for mutation.
    /// </summary>
    /// <returns></returns>
    public ToolCallContext Clone()
    {
        var clone = new ToolCallContext
        {
            Prompt = Prompt,
            Tools = Tools.ToList(),
            PreferredFormat = PreferredFormat,
            Temperature = Temperature,
            Generation = Generation,
        };
        clone.History.AddRange(History);
        return clone;
    }
}

/// <summary>
/// Slim tool definition for mutation context. Avoids coupling to MCP or Ollama types.
/// </summary>
/// <param name="Name">The tool name.</param>
/// <param name="Description">The tool description.</param>
/// <param name="JsonSchema">The input schema as JSON string.</param>
public sealed record ToolDefinitionSlim(string Name, string Description, string? JsonSchema);

/// <summary>
/// Records a mutation applied during evolutionary retry.
/// </summary>
/// <param name="StrategyName">The strategy that produced the mutation.</param>
/// <param name="Generation">The generation at which this mutation was applied.</param>
/// <param name="Error">The error that triggered the mutation.</param>
/// <param name="Timestamp">When the mutation was applied.</param>
public sealed record MutationHistoryEntry(
    string StrategyName,
    int Generation,
    Exception Error,
    DateTime Timestamp);
