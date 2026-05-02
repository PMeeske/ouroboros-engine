// <copyright file="IMeTTaRulePersistence.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.Persistence;

/// <summary>
/// Persistence contract for learned MeTTa rules.
/// </summary>
public interface IMeTTaRulePersistence
{
    /// <summary>
    /// Persists a single rule.
    /// </summary>
    /// <param name="rule">The rule to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success/failure.</returns>
    Task<Result<Unit, string>> PersistAsync(MeTTaRule rule, CancellationToken ct = default);

    /// <summary>
    /// Restores all rules previously persisted under a session id.
    /// </summary>
    /// <param name="sessionId">The session to restore.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The restored rules ordered by step ascending.</returns>
    Task<Result<IReadOnlyList<MeTTaRule>, string>> RestoreAsync(string sessionId, CancellationToken ct = default);
}
