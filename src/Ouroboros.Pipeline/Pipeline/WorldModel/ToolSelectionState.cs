using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Pipeline.WorldModel;

/// <summary>
/// Represents the state of a tool selection reasoning step.
/// </summary>
/// <param name="Goal">The goal that tools were selected for.</param>
/// <param name="Selection">The resulting tool selection.</param>
/// <param name="Timestamp">When the selection was made.</param>
public sealed record ToolSelectionState(
    Goal Goal,
    ToolSelection Selection,
    DateTime Timestamp) : ReasoningState(
    Kind: "ToolSelection",
    Text: $"Selected {Selection.SelectedTools.Count} tools for '{Goal.Description}' (confidence: {Selection.ConfidenceScore:P0})")
{
    /// <summary>
    /// Gets a summary of this selection state.
    /// </summary>
    /// <returns>A human-readable summary.</returns>
    public string GetSummary() =>
        $"Selected {Selection.SelectedTools.Count} tools for '{Goal.Description}' (confidence: {Selection.ConfidenceScore:P0})";
}