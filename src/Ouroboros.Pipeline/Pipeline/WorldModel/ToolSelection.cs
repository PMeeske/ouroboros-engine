namespace Ouroboros.Pipeline.WorldModel;

/// <summary>
/// Represents the result of a tool selection process.
/// </summary>
/// <param name="SelectedTools">The tools selected for the goal.</param>
/// <param name="Reasoning">Human-readable explanation of the selection.</param>
/// <param name="ConfidenceScore">Overall confidence in the selection (0.0 to 1.0).</param>
/// <param name="AllCandidates">All evaluated candidates before filtering.</param>
/// <param name="AppliedConstraints">Constraints that were applied during selection.</param>
public sealed record ToolSelection(
    IReadOnlyList<ITool> SelectedTools,
    string Reasoning,
    double ConfidenceScore,
    IReadOnlyList<ToolCandidate> AllCandidates,
    IReadOnlyList<Constraint> AppliedConstraints)
{
    /// <summary>
    /// Gets an empty selection with no tools.
    /// </summary>
    public static ToolSelection Empty { get; } = new(
        SelectedTools: [],
        Reasoning: "No tools selected.",
        ConfidenceScore: 0.0,
        AllCandidates: [],
        AppliedConstraints: []);

    /// <summary>
    /// Creates a failed selection result.
    /// </summary>
    /// <param name="reason">The reason for failure.</param>
    /// <returns>A failed selection result.</returns>
    public static ToolSelection Failed(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        return new ToolSelection(
            SelectedTools: [],
            Reasoning: reason,
            ConfidenceScore: 0.0,
            AllCandidates: [],
            AppliedConstraints: []);
    }

    /// <summary>
    /// Checks if any tools were selected.
    /// </summary>
    public bool HasTools => SelectedTools.Count > 0;

    /// <summary>
    /// Gets the tool names as an immutable set.
    /// </summary>
    public IReadOnlySet<string> ToolNames =>
        SelectedTools.Select(t => t.Name).ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new selection with an additional constraint recorded.
    /// </summary>
    /// <param name="constraint">The constraint to add.</param>
    /// <returns>A new selection with the constraint added.</returns>
    public ToolSelection WithAppliedConstraint(Constraint constraint)
    {
        ArgumentNullException.ThrowIfNull(constraint);

        return this with { AppliedConstraints = AppliedConstraints.Append(constraint).ToList() };
    }
}