// <copyright file="IAgentProviderFactory.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.MeTTaAgents;

/// <summary>
/// Factory that creates chat completion model instances from MeTTa agent definitions.
/// The MeTTa AtomSpace declares agents; this factory materializes them into
/// provider-backed <see cref="Ouroboros.Abstractions.Core.IChatCompletionModel"/> instances.
/// </summary>
public interface IAgentProviderFactory
{
    /// <summary>
    /// Checks if this factory handles the given provider type.
    /// </summary>
    /// <param name="providerName">The provider name from MeTTa (e.g., "Ollama", "OllamaCloud").</param>
    /// <returns>True if this factory can create models for the provider.</returns>
    bool CanHandle(string providerName);

    /// <summary>
    /// Creates a chat completion model from a MeTTa agent definition.
    /// </summary>
    /// <param name="agentDef">The agent definition containing provider, model, and configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the chat model on success, or an error message on failure.</returns>
    Task<Result<Ouroboros.Abstractions.Core.IChatCompletionModel, string>> CreateModelAsync(
        MeTTaAgentDef agentDef,
        CancellationToken ct = default);

    /// <summary>
    /// Performs a health check on the provider endpoint.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the health status on success, or an error message on failure.</returns>
    Task<Result<ProviderHealthStatus, string>> HealthCheckAsync(
        CancellationToken ct = default);
}
