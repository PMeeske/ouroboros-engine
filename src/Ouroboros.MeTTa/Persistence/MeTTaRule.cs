// <copyright file="MeTTaRule.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.Persistence;

/// <summary>
/// A learned MeTTa rule, persisted across sessions.
/// </summary>
/// <param name="AtomText">The S-expression form of the rule.</param>
/// <param name="SessionId">The session that generated the rule.</param>
/// <param name="Step">Step counter within the session.</param>
/// <param name="QualityScore">A 0..1 score from the rule's evaluation harness.</param>
/// <param name="Timestamp">UTC timestamp when the rule was emitted.</param>
public sealed record MeTTaRule(
    string AtomText,
    string SessionId,
    int Step,
    double QualityScore,
    DateTimeOffset Timestamp);
