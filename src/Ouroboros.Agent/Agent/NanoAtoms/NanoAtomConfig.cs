// <copyright file="NanoAtomConfig.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.NanoAtoms;

/// <summary>
/// Configuration for NanoOuroborosAtom processing, including token budgets,
/// parallelism, circuit breaker settings, and self-critique behavior.
/// </summary>
/// <param name="MaxInputTokens">Maximum input tokens per atom (default 512).</param>
/// <param name="MaxOutputTokens">Maximum output tokens per atom (default 256).</param>
/// <param name="DigestTargetTokens">Target token count for self-consumed digest (default 128).</param>
/// <param name="DigestPrompt">Prompt template for the self-consumption step. {0}=target tokens, {1}=content.</param>
/// <param name="ProcessPrompt">Prompt template for the processing step. {0}=content.</param>
/// <param name="MaxParallelAtoms">Maximum number of parallel atom streams (default 4).</param>
/// <param name="ConsolidationThreshold">Minimum aggregate confidence to emit a ConsolidatedAction (default 0.6).</param>
/// <param name="EnableSelfCritique">Whether to run mini SelfCritique in the digest step (default true).</param>
/// <param name="EnableCircuitBreaker">Whether to wrap LLM calls with Polly circuit breaker (default true).</param>
/// <param name="CircuitBreakerFailureThreshold">Consecutive failures before circuit opens (default 3).</param>
/// <param name="UseGoalDecomposer">Whether to use GoalDecomposer for intelligent splitting (default true).</param>
/// <param name="AtomTimeout">Optional timeout per atom operation.</param>
public sealed record NanoAtomConfig(
    int MaxInputTokens = 512,
    int MaxOutputTokens = 256,
    int DigestTargetTokens = 128,
    string DigestPrompt = "Compress the following to its essential meaning in under {0} tokens:\n\n{1}",
    string ProcessPrompt = "Given this thought, provide a focused response:\n\n{0}",
    int MaxParallelAtoms = 4,
    double ConsolidationThreshold = 0.6,
    bool EnableSelfCritique = true,
    bool EnableCircuitBreaker = true,
    int CircuitBreakerFailureThreshold = 3,
    bool UseGoalDecomposer = true,
    TimeSpan? AtomTimeout = null)
{
    /// <summary>
    /// Minimal configuration for fastest processing with smallest models.
    /// 256 input / 128 output / 64 digest, no self-critique, no circuit breaker.
    /// </summary>
    public static NanoAtomConfig Minimal() => new(
        MaxInputTokens: 256,
        MaxOutputTokens: 128,
        DigestTargetTokens: 64,
        MaxParallelAtoms: 2,
        EnableSelfCritique: false,
        EnableCircuitBreaker: false,
        UseGoalDecomposer: false);

    /// <summary>
    /// Default balanced configuration.
    /// </summary>
    public static NanoAtomConfig Default() => new();

    /// <summary>
    /// High-quality configuration with larger budgets and all features enabled.
    /// </summary>
    public static NanoAtomConfig HighQuality() => new(
        MaxInputTokens: 1024,
        MaxOutputTokens: 512,
        DigestTargetTokens: 256,
        MaxParallelAtoms: 6,
        EnableSelfCritique: true,
        EnableCircuitBreaker: true,
        UseGoalDecomposer: true);
}
