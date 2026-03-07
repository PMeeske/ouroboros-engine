namespace Ouroboros.Agent.Cognition.Planning;

/// <summary>
/// Context for Hypergrid dimensional routing (Whitepaper Section 7).
/// Carries the available dimensional axes and routing policies.
/// </summary>
public sealed record HypergridContext(
    DateTimeOffset? Deadline,
    IReadOnlyList<string> AvailableSkills,
    IReadOnlyList<string> AvailableTools,
    double RiskThreshold = 0.7)
{
    public static HypergridContext Default { get; } = new(
        Deadline: null,
        AvailableSkills: Array.Empty<string>(),
        AvailableTools: Array.Empty<string>());
}

/// <summary>
/// N-dimensional coordinate in the Hypergrid thought-space.
/// Each axis carries domain-specific semantics (Section 7.2).
/// </summary>
public sealed record DimensionalCoordinate(
    double Temporal,
    double Semantic,
    double Causal,
    double Modal)
{
    /// <summary>Origin point — no dimensional bias.</summary>
    public static DimensionalCoordinate Origin { get; } = new(0, 0, 0, 0);

    /// <summary>Euclidean distance to another coordinate.</summary>
    public double DistanceTo(DimensionalCoordinate other)
    {
        double dt = Temporal - other.Temporal;
        double ds = Semantic - other.Semantic;
        double dc = Causal - other.Causal;
        double dm = Modal - other.Modal;
        return Math.Sqrt(dt * dt + ds * ds + dc * dc + dm * dm);
    }
}

/// <summary>
/// Result of analyzing a goal decomposition across Hypergrid dimensions.
/// </summary>
public sealed record HypergridAnalysis(
    double TemporalSpan,
    double SemanticBreadth,
    int CausalDepth,
    IReadOnlyList<string> ModalRequirements,
    double OverallComplexity)
{
    public HypergridAnalysis()
        : this(0, 0, 0, Array.Empty<string>(), 0) { }
}
