namespace Ouroboros.Pipeline.Planning;

/// <summary>
/// Represents a tool chain derived from MeTTa backward chaining.
/// </summary>
/// <param name="Tools">The ordered list of tools to execute.</param>
public sealed record ToolChain(IReadOnlyList<string> Tools)
{
    /// <summary>
    /// Gets whether this chain is empty.
    /// </summary>
    public bool IsEmpty => this.Tools.Count == 0;

    /// <summary>
    /// Creates an empty tool chain.
    /// </summary>
    public static ToolChain Empty => new(Array.Empty<string>());
}