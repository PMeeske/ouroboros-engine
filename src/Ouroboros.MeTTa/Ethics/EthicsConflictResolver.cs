// <copyright file="EthicsConflictResolver.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.Ethics;

/// <summary>
/// Resolves conflicts between ethical traditions via weighted voting,
/// with a confidence-threshold human-escalation fallback.
/// </summary>
/// <remarks>
/// <para>
/// Each tradition emits an <see cref="EthicsVote"/> for a candidate action.
/// The resolver aggregates votes weighted by tradition weight and a
/// per-vote confidence. If the resulting confidence (in 0..1) falls below
/// <see cref="ResolutionPolicy.HumanEscalationThreshold"/>, the resolver
/// emits an <see cref="EthicsResolution"/> with
/// <see cref="EthicsResolution.RequiresHumanEscalation"/> set to <c>true</c>.
/// </para>
/// <para>
/// Optionally accepts a phi-proxy from <see cref="MeTTaConsciousnessMemory"/>:
/// when phi is low (poor cross-tradition integration), the effective
/// confidence is dampened, biasing decisions toward escalation.
/// </para>
/// </remarks>
public sealed class EthicsConflictResolver
{
    private readonly ResolutionPolicy _policy;

    /// <summary>
    /// Initializes a new instance with the default resolution policy.
    /// </summary>
    public EthicsConflictResolver()
        : this(ResolutionPolicy.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance with an explicit policy.
    /// </summary>
    /// <param name="policy">The resolution policy to apply.</param>
    public EthicsConflictResolver(ResolutionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policy = policy;
    }

    /// <summary>
    /// Resolves a set of votes into a single decision.
    /// </summary>
    /// <param name="votes">The votes from each consulted tradition.</param>
    /// <param name="phiProxy">
    /// Optional integrated-information proxy in 0..1. Pass 1.0 to disable
    /// dampening (no consciousness-aware adjustment).
    /// </param>
    /// <returns>The aggregated resolution.</returns>
    public EthicsResolution Resolve(IReadOnlyList<EthicsVote> votes, double phiProxy = 1.0)
    {
        ArgumentNullException.ThrowIfNull(votes);

        if (votes.Count == 0)
        {
            return new EthicsResolution(
                Verdict: EthicsVerdict.Indeterminate,
                Confidence: 0.0,
                RequiresHumanEscalation: true,
                ContributingTraditions: Array.Empty<EthicsTradition>(),
                Rationale: "No votes — escalating to human.");
        }

        double clampedPhi = Math.Clamp(phiProxy, 0.0, 1.0);

        double approveWeight = 0.0;
        double rejectWeight = 0.0;
        double abstainWeight = 0.0;
        double totalWeight = 0.0;

        foreach (EthicsVote v in votes)
        {
            double traditionWeight = _policy.TraditionWeights.TryGetValue(v.Tradition, out double w)
                ? w
                : _policy.DefaultTraditionWeight;

            double effective = traditionWeight * Math.Clamp(v.Confidence, 0.0, 1.0);
            totalWeight += effective;

            switch (v.Verdict)
            {
                case EthicsVerdict.Approve:
                    approveWeight += effective;
                    break;
                case EthicsVerdict.Reject:
                    rejectWeight += effective;
                    break;
                default:
                    abstainWeight += effective;
                    break;
            }
        }

        if (totalWeight <= 0.0)
        {
            return new EthicsResolution(
                EthicsVerdict.Indeterminate,
                0.0,
                RequiresHumanEscalation: true,
                ContributingTraditions: votes.Select(v => v.Tradition).Distinct().ToImmutableArray(),
                Rationale: "All votes had zero weight or zero confidence — escalating.");
        }

        double approveShare = approveWeight / totalWeight;
        double rejectShare = rejectWeight / totalWeight;

        EthicsVerdict verdict;
        double margin;

        if (approveShare >= rejectShare && approveShare > _policy.MinimumMajorityShare)
        {
            verdict = EthicsVerdict.Approve;
            margin = approveShare - rejectShare;
        }
        else if (rejectShare > approveShare && rejectShare > _policy.MinimumMajorityShare)
        {
            verdict = EthicsVerdict.Reject;
            margin = rejectShare - approveShare;
        }
        else
        {
            verdict = EthicsVerdict.Indeterminate;
            margin = 0.0;
        }

        // Confidence is the winning margin scaled by phi.
        double confidence = Math.Clamp(margin * clampedPhi, 0.0, 1.0);

        bool needsEscalation = verdict == EthicsVerdict.Indeterminate
            || confidence < _policy.HumanEscalationThreshold;

        IReadOnlyList<EthicsTradition> contributing = votes
            .Select(v => v.Tradition)
            .Distinct()
            .ToImmutableArray();

        string rationale = $"Approve={approveShare:F3} Reject={rejectShare:F3} Abstain={abstainWeight / totalWeight:F3} phi={clampedPhi:F3}";

        return new EthicsResolution(verdict, confidence, needsEscalation, contributing, rationale);
    }
}

/// <summary>
/// A single tradition's vote on a candidate action.
/// </summary>
/// <param name="Tradition">The tradition casting the vote.</param>
/// <param name="Verdict">Approve / Reject / Abstain.</param>
/// <param name="Confidence">The tradition's own confidence in 0..1.</param>
public sealed record EthicsVote(
    EthicsTradition Tradition,
    EthicsVerdict Verdict,
    double Confidence);

/// <summary>
/// The 3-valued ethics verdict.
/// </summary>
public enum EthicsVerdict
{
    /// <summary>The action is permitted.</summary>
    Approve,

    /// <summary>The action is forbidden.</summary>
    Reject,

    /// <summary>No clear position — abstain or indeterminate.</summary>
    Indeterminate,
}

/// <summary>
/// The aggregated resolution of an ethics-vote set.
/// </summary>
/// <param name="Verdict">The aggregate verdict.</param>
/// <param name="Confidence">Aggregate confidence in 0..1.</param>
/// <param name="RequiresHumanEscalation">True when policy demands a human decision.</param>
/// <param name="ContributingTraditions">The traditions whose votes contributed.</param>
/// <param name="Rationale">Human-readable share breakdown.</param>
public sealed record EthicsResolution(
    EthicsVerdict Verdict,
    double Confidence,
    bool RequiresHumanEscalation,
    IReadOnlyList<EthicsTradition> ContributingTraditions,
    string Rationale);

/// <summary>
/// Tunable parameters for <see cref="EthicsConflictResolver"/>.
/// </summary>
public sealed class ResolutionPolicy
{
    /// <summary>
    /// Gets the default policy: 1.0 weight per tradition, 0.5 minimum
    /// majority share, 0.4 escalation threshold.
    /// </summary>
    public static ResolutionPolicy Default { get; } = new();

    /// <summary>
    /// Gets per-tradition weights. Missing entries fall back to
    /// <see cref="DefaultTraditionWeight"/>.
    /// </summary>
    public IReadOnlyDictionary<EthicsTradition, double> TraditionWeights { get; init; }
        = ImmutableDictionary<EthicsTradition, double>.Empty;

    /// <summary>
    /// Gets the weight assigned to traditions not explicitly listed in
    /// <see cref="TraditionWeights"/>.
    /// </summary>
    public double DefaultTraditionWeight { get; init; } = 1.0;

    /// <summary>
    /// Gets the share of total weight a verdict must exceed to be selected.
    /// Values at or below 0.5 admit pure pluralities; higher values
    /// require super-majority agreement.
    /// </summary>
    public double MinimumMajorityShare { get; init; } = 0.5;

    /// <summary>
    /// Gets the confidence threshold below which the resolver demands
    /// human escalation regardless of the winning verdict.
    /// </summary>
    public double HumanEscalationThreshold { get; init; } = 0.4;
}
