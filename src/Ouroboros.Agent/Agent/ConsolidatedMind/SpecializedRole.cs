// <copyright file="SpecializedRole.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.ConsolidatedMind;

/// <summary>
/// Defines specialized roles that sub-models can fulfill within the ConsolidatedMind.
/// Each role represents a distinct cognitive capability.
/// </summary>
public enum SpecializedRole
{
    /// <summary>
    /// Fast responder for simple queries and quick answers.
    /// Uses lightweight models optimized for speed.
    /// </summary>
    QuickResponse,

    /// <summary>
    /// Deep reasoning and logical analysis.
    /// Uses models optimized for chain-of-thought reasoning.
    /// </summary>
    DeepReasoning,

    /// <summary>
    /// Code generation, analysis, and debugging.
    /// Uses models trained specifically on code.
    /// </summary>
    CodeExpert,

    /// <summary>
    /// Creative writing, brainstorming, and ideation.
    /// Uses models with high creativity/temperature settings.
    /// </summary>
    Creative,

    /// <summary>
    /// Mathematical computations and formal logic.
    /// Uses models trained on mathematical reasoning.
    /// </summary>
    Mathematical,

    /// <summary>
    /// Analysis, critique, and evaluation of content.
    /// Uses models optimized for analytical tasks.
    /// </summary>
    Analyst,

    /// <summary>
    /// Synthesis and summarization of information.
    /// Uses models optimized for compression and extraction.
    /// </summary>
    Synthesizer,

    /// <summary>
    /// Planning and decomposition of complex tasks.
    /// Uses models with strong planning capabilities.
    /// </summary>
    Planner,

    /// <summary>
    /// Verification and fact-checking.
    /// Uses models for validation and consistency checks.
    /// </summary>
    Verifier,

    /// <summary>
    /// Meta-cognition and self-reflection.
    /// Orchestrates other models and makes routing decisions.
    /// </summary>
    MetaCognitive,

    /// <summary>
    /// Symbolic reasoning using MeTTa/Hyperon engine.
    /// Provides deterministic logic-based reasoning without LLM dependency.
    /// Acts as ultimate fallback when all neural models are unavailable.
    /// </summary>
    SymbolicReasoner
}