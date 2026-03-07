using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Cognition.Planning;

/// <summary>
/// Routes sub-goals through Hypergrid dimensional axes (Whitepaper Section 7).
/// Assigns each goal step a coordinate in N-dimensional thought-space based on:
///   d0 Temporal  — urgency and sequencing
///   d1 Semantic  — domain/skill affinity
///   d2 Causal    — dependency depth
///   d3 Modal     — execution mode (automatic, approval, delegation)
/// </summary>
public sealed class HypergridRouter
{
    /// <summary>
    /// Assigns dimensional coordinates and execution modes to a list of raw goal steps.
    /// Returns annotated steps plus an overall analysis.
    /// </summary>
    public (IReadOnlyList<GoalStep> Steps, HypergridAnalysis Analysis) Route(
        IReadOnlyList<RawGoalStep> rawSteps,
        HypergridContext context)
    {
        // Build causal ordering (dependency graph depth)
        var depthMap = ComputeCausalDepth(rawSteps);
        int maxDepth = depthMap.Values.DefaultIfEmpty(0).Max();

        var steps = new List<GoalStep>(rawSteps.Count);

        for (int i = 0; i < rawSteps.Count; i++)
        {
            var raw = rawSteps[i];
            int causalDepth = depthMap.GetValueOrDefault(raw.Id, 0);

            double temporal = ComputeTemporal(i, rawSteps.Count, context);
            double semantic = ComputeSemantic(raw, context);
            double causal = maxDepth > 0 ? (double)causalDepth / maxDepth : 0;
            double modal = ComputeModal(raw, context);

            var coordinate = new DimensionalCoordinate(temporal, semantic, causal, modal);
            var mode = DetermineExecutionMode(raw, modal, context);

            steps.Add(new GoalStep(
                raw.Id,
                raw.Description,
                raw.Type,
                raw.Priority,
                coordinate,
                raw.DependsOn,
                mode));
        }

        var analysis = new HypergridAnalysis(
            TemporalSpan: steps.Count > 1
                ? steps.Max(s => s.Coordinate.Temporal) - steps.Min(s => s.Coordinate.Temporal)
                : 0,
            SemanticBreadth: steps.Count > 1
                ? steps.Max(s => s.Coordinate.Semantic) - steps.Min(s => s.Coordinate.Semantic)
                : 0,
            CausalDepth: maxDepth,
            ModalRequirements: steps
                .Where(s => s.Mode != ExecutionMode.Automatic)
                .Select(s => $"{s.Description} -> {s.Mode}")
                .ToArray(),
            OverallComplexity: ComputeComplexity(steps));

        return (steps, analysis);
    }

    // -- Temporal axis (d0): position in execution sequence + deadline pressure --

    private static double ComputeTemporal(int index, int total, HypergridContext context)
    {
        double sequencePosition = total > 1 ? (double)index / (total - 1) : 0;

        if (context.Deadline is { } deadline)
        {
            double hoursRemaining = (deadline - DateTimeOffset.UtcNow).TotalHours;
            double urgency = Math.Clamp(1.0 - hoursRemaining / 168.0, 0, 1); // 168h = 1 week
            return (sequencePosition + urgency) / 2.0;
        }

        return sequencePosition;
    }

    // -- Semantic axis (d1): how well the step matches available skills --

    private static double ComputeSemantic(RawGoalStep step, HypergridContext context)
    {
        if (context.AvailableSkills.Count == 0)
            return 0.5; // neutral

        string lower = step.Description.ToLowerInvariant();
        int matchCount = context.AvailableSkills
            .Count(skill => lower.Contains(skill.ToLowerInvariant()));

        return Math.Clamp((double)matchCount / Math.Max(context.AvailableSkills.Count, 1), 0, 1);
    }

    // -- Causal axis (d2): dependency depth in the DAG --

    private static Dictionary<Guid, int> ComputeCausalDepth(IReadOnlyList<RawGoalStep> steps)
    {
        var depthMap = new Dictionary<Guid, int>();
        var stepLookup = steps.ToDictionary(s => s.Id);

        int GetDepth(Guid id, HashSet<Guid> visited)
        {
            if (depthMap.TryGetValue(id, out int cached)) return cached;
            if (!stepLookup.TryGetValue(id, out var step)) return 0;
            if (!visited.Add(id)) return 0; // cycle guard

            int maxParentDepth = 0;
            foreach (var depId in step.DependsOn)
            {
                maxParentDepth = Math.Max(maxParentDepth, GetDepth(depId, visited) + 1);
            }

            depthMap[id] = maxParentDepth;
            return maxParentDepth;
        }

        foreach (var step in steps)
            GetDepth(step.Id, new HashSet<Guid>());

        return depthMap;
    }

    // -- Modal axis (d3): execution mode based on risk and tool availability --

    private static double ComputeModal(RawGoalStep step, HypergridContext context)
    {
        // High priority + safety type = high modal value (needs oversight)
        double riskSignal = step.Type == GoalType.Safety ? 1.0
                          : step.Priority > 0.8 ? 0.7
                          : step.Priority > 0.5 ? 0.4
                          : 0.1;

        // Check if tools are available for delegation
        string lower = step.Description.ToLowerInvariant();
        bool hasTool = context.AvailableTools
            .Any(t => lower.Contains(t.ToLowerInvariant()));

        if (hasTool) riskSignal *= 0.5; // tools reduce modal complexity

        return Math.Clamp(riskSignal, 0, 1);
    }

    private static ExecutionMode DetermineExecutionMode(
        RawGoalStep step, double modal, HypergridContext context)
    {
        if (step.Type == GoalType.Safety || modal > context.RiskThreshold)
            return ExecutionMode.RequiresApproval;

        string lower = step.Description.ToLowerInvariant();
        bool hasTool = context.AvailableTools
            .Any(t => lower.Contains(t.ToLowerInvariant()));

        if (hasTool)
            return ExecutionMode.ToolDelegation;

        return ExecutionMode.Automatic;
    }

    private static double ComputeComplexity(List<GoalStep> steps)
    {
        if (steps.Count == 0) return 0;

        double avgDistance = 0;
        for (int i = 1; i < steps.Count; i++)
            avgDistance += steps[i].Coordinate.DistanceTo(steps[i - 1].Coordinate);

        avgDistance /= Math.Max(steps.Count - 1, 1);

        // Complexity = step count factor * dimensional spread
        return Math.Clamp(steps.Count / 10.0 + avgDistance, 0, 1);
    }
}

/// <summary>
/// Raw goal step before Hypergrid routing — produced by the SK planner.
/// </summary>
public sealed record RawGoalStep(
    Guid Id,
    string Description,
    GoalType Type,
    double Priority,
    IReadOnlyList<Guid> DependsOn)
{
    public RawGoalStep(string description, GoalType type, double priority)
        : this(Guid.NewGuid(), description, type, priority, Array.Empty<Guid>()) { }
}
