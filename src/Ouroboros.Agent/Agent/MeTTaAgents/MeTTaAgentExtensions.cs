// <copyright file="MeTTaAgentExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Agent.ConsolidatedMind;
using Ouroboros.Providers.Configuration;

namespace Ouroboros.Agent.MeTTaAgents;

/// <summary>
/// Extension methods for integrating MeTTa-spawned agents with
/// ConsolidatedMind and other orchestration systems.
/// </summary>
public static class MeTTaAgentExtensions
{
    /// <summary>
    /// Registers all spawned MeTTa agents as specialists in the ConsolidatedMind.
    /// Agents are mapped from their MeTTa roles to <see cref="SpecializedRole"/> values.
    /// </summary>
    /// <param name="mind">The ConsolidatedMind to register agents with.</param>
    /// <param name="runtime">The agent runtime containing spawned agents.</param>
    /// <returns>The number of agents registered.</returns>
    public static int RegisterMeTTaAgents(
        this ConsolidatedMind.ConsolidatedMind mind,
        MeTTaAgentRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(mind);
        ArgumentNullException.ThrowIfNull(runtime);

        int registered = 0;

        foreach (var (_, agent) in runtime.SpawnedAgents)
        {
            var role = AgentRoleMapping.ToSpecializedRole(agent.Definition.Role);
            if (role == null) continue;

            var specialist = new SpecializedModel(
                role.Value,
                agent.Model,
                $"{agent.Definition.Provider}/{agent.Definition.Model}",
                agent.Definition.Capabilities?.ToArray() ?? Array.Empty<string>(),
                Priority: 5.0,
                MaxTokens: agent.Definition.MaxTokens);

            mind.RegisterSpecialist(specialist);
            registered++;
        }

        return registered;
    }

    /// <summary>
    /// Creates a default set of provider factories including Ollama, ONNX GenAI, ONNX Tool, and Mock providers.
    /// </summary>
    /// <param name="ollamaEndpoint">Optional Ollama endpoint override.</parameter>
    /// <param name="onnxModelBasePath">Optional base path for ONNX model directories.</parameter>
    /// <returns>List of provider factories.</returns>
    public static IReadOnlyList<IAgentProviderFactory> CreateDefaultProviders(
        string ollamaEndpoint = DefaultEndpoints.Ollama,
        string? onnxModelBasePath = null)
    {
        var providers = new List<IAgentProviderFactory>
        {
            new OllamaAgentProvider(ollamaEndpoint),
            new MockAgentProvider()
        };
#if ENABLE_ONNX_PROVIDERS
        if (!string.IsNullOrEmpty(onnxModelBasePath))
        {
            providers.Add(new OnnxGenAiAgentProvider(onnxModelBasePath));
            providers.Add(new OnnxToolAgentProvider(onnxModelBasePath));
        }
#endif
        return providers;
    }

    /// <summary>
    /// Creates a MeTTaAgentRuntime with the default set of providers.
    /// </summary>
    /// <param name="engine">The MeTTa engine.</parameter>
    /// <param name="ollamaEndpoint">Optional Ollama endpoint override.</parameter>
    /// <param name="onnxModelBasePath">Optional base path for ONNX model directories.</parameter>
    /// <returns>A configured MeTTaAgentRuntime.</returns>
    public static MeTTaAgentRuntime CreateDefaultRuntime(
        IMeTTaEngine engine,
        string ollamaEndpoint = DefaultEndpoints.Ollama,
        string? onnxModelBasePath = null)
    {
        return new MeTTaAgentRuntime(engine, CreateDefaultProviders(ollamaEndpoint, onnxModelBasePath));
    }
}
