namespace Ouroboros.Pipeline.Verification;

/// <summary>
/// Represents a tool invocation action.
/// </summary>
/// <param name="ToolName">The name of the tool to invoke.</param>
/// <param name="Arguments">The arguments to pass to the tool.</param>
public sealed record ToolAction(string ToolName, string? Arguments = null) : PlanAction
{
    /// <inheritdoc/>
    public override string ToMeTTaAtom() => $"(ToolAction \"{ToolName}\")";
}