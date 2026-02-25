// <copyright file="MeTTaAgentTypes.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.MeTTaAgents;

/// <summary>
/// MeTTa agent definition extracted from the AtomSpace.
/// Represents the declarative blueprint for a sub-agent.
/// </summary>
/// <param name="AgentId">Unique identifier for this agent.</param>
/// <param name="Provider">The provider backend (e.g., "Ollama", "OllamaCloud", "OpenAI").</param>
/// <param name="Model">The model identifier (e.g., "deepseek-coder-v2:236b").</param>
/// <param name="Role">The agent's role (e.g., "Coder", "Reviewer", "Planner").</param>
/// <param name="SystemPrompt">System prompt that defines the agent's behavior.</param>
/// <param name="MaxTokens">Maximum tokens for generation.</param>
/// <param name="Temperature">Temperature for generation (0.0-1.0).</param>
/// <param name="Endpoint">Optional override endpoint for the provider.</param>
/// <param name="ApiKeyEnvVar">Optional environment variable name for API key.</param>
/// <param name="Capabilities">List of capabilities this agent has.</param>
public sealed record MeTTaAgentDef(
    string AgentId,
    string Provider,
    string Model,
    string Role,
    string SystemPrompt,
    int MaxTokens,
    float Temperature,
    string? Endpoint = null,
    string? ApiKeyEnvVar = null,
    IReadOnlyList<string>? Capabilities = null);

/// <summary>
/// Health status of a provider endpoint.
/// </summary>
/// <param name="ProviderName">Name of the provider.</param>
/// <param name="IsHealthy">Whether the provider is responding normally.</param>
/// <param name="LatencyMs">Last measured latency in milliseconds.</param>
/// <param name="ErrorMessage">Optional error message if unhealthy.</param>
public sealed record ProviderHealthStatus(
    string ProviderName,
    bool IsHealthy,
    double LatencyMs,
    string? ErrorMessage = null);

/// <summary>
/// A materialized agent instance backed by a provider model.
/// </summary>
/// <param name="Definition">The MeTTa agent definition that created this instance.</param>
/// <param name="Model">The chat completion model backing this agent.</param>
/// <param name="SpawnedAt">When this agent was spawned.</param>
public sealed record SpawnedAgent(
    MeTTaAgentDef Definition,
    Ouroboros.Abstractions.Core.IChatCompletionModel Model,
    DateTime SpawnedAt);

/// <summary>
/// Status of agent operations returned from the agent tool.
/// </summary>
/// <param name="AgentId">The agent identifier.</param>
/// <param name="Status">Current status string.</param>
/// <param name="Message">Descriptive message about the operation.</param>
/// <param name="Timestamp">When this status was generated.</param>
public sealed record AgentOperationStatus(
    string AgentId,
    string Status,
    string Message,
    DateTime Timestamp);

/// <summary>
/// Maps MeTTa agent roles to ConsolidatedMind specialized roles.
/// </summary>
public static class AgentRoleMapping
{
    /// <summary>
    /// Maps a MeTTa agent role string to a <see cref="ConsolidatedMind.SpecializedRole"/>.
    /// </summary>
    /// <param name="mettaRole">The role string from MeTTa (e.g., "Coder", "Reviewer").</param>
    /// <returns>The corresponding specialized role, or null if no mapping exists.</returns>
    public static ConsolidatedMind.SpecializedRole? ToSpecializedRole(string mettaRole)
    {
        return mettaRole switch
        {
            "Coder" => ConsolidatedMind.SpecializedRole.CodeExpert,
            "Reviewer" => ConsolidatedMind.SpecializedRole.Verifier,
            "Planner" => ConsolidatedMind.SpecializedRole.Planner,
            "Reasoner" => ConsolidatedMind.SpecializedRole.DeepReasoning,
            "Researcher" => ConsolidatedMind.SpecializedRole.Analyst,
            "Summarizer" => ConsolidatedMind.SpecializedRole.Synthesizer,
            "SecurityAuditor" => ConsolidatedMind.SpecializedRole.Analyst,
            "Critic" => ConsolidatedMind.SpecializedRole.Verifier,
            "Synthesizer" => ConsolidatedMind.SpecializedRole.Synthesizer,
            _ => null
        };
    }
}
