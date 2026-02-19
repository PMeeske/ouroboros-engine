// <copyright file="OllamaAgentProvider.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using LangChain.Providers.Ollama;
using Ouroboros.Providers;

namespace Ouroboros.Agent.MeTTaAgents;

/// <summary>
/// Creates Ollama-backed agents from MeTTa definitions.
/// Supports both local Ollama and Ollama Cloud endpoints.
/// </summary>
public sealed class OllamaAgentProvider : IAgentProviderFactory
{
    private readonly string _defaultEndpoint;
    private OllamaProvider? _provider;

    /// <summary>
    /// Creates a new Ollama agent provider.
    /// </summary>
    /// <param name="defaultEndpoint">Default Ollama endpoint URL.</param>
    public OllamaAgentProvider(string defaultEndpoint = "http://localhost:11434")
    {
        _defaultEndpoint = defaultEndpoint;
    }

    /// <inheritdoc/>
    public bool CanHandle(string providerName)
        => providerName is "Ollama" or "OllamaCloud";

    /// <inheritdoc/>
    public Task<Result<Ouroboros.Abstractions.Core.IChatCompletionModel, string>> CreateModelAsync(
        MeTTaAgentDef agentDef, CancellationToken ct = default)
    {
        try
        {
            string endpoint = agentDef.Endpoint ?? _defaultEndpoint;

            if (agentDef.Provider == "OllamaCloud")
            {
                return Task.FromResult(CreateCloudModel(agentDef, endpoint));
            }

            // Local Ollama via LangChain adapter
            _provider ??= new OllamaProvider(endpoint);
            var langChainModel = new OllamaChatModel(_provider, agentDef.Model);
            var adapter = new OllamaChatAdapter(langChainModel);
            return Task.FromResult(
                Result<Ouroboros.Abstractions.Core.IChatCompletionModel, string>.Success(adapter));
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                Result<Ouroboros.Abstractions.Core.IChatCompletionModel, string>.Failure(
                    $"Failed to create Ollama agent '{agentDef.AgentId}': {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public async Task<Result<ProviderHealthStatus, string>> HealthCheckAsync(
        CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var resp = await http.GetAsync($"{_defaultEndpoint}/api/tags", ct);
            sw.Stop();
            return Result<ProviderHealthStatus, string>.Success(
                new ProviderHealthStatus("Ollama", resp.IsSuccessStatusCode, sw.ElapsedMilliseconds));
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Result<ProviderHealthStatus, string>.Success(
                new ProviderHealthStatus("Ollama", false, sw.ElapsedMilliseconds, ex.Message));
        }
    }

    private static Result<Ouroboros.Abstractions.Core.IChatCompletionModel, string> CreateCloudModel(
        MeTTaAgentDef agentDef, string endpoint)
    {
        string? apiKeyEnvVar = agentDef.ApiKeyEnvVar ?? "OLLAMA_CLOUD_API_KEY";
        string? apiKey = Environment.GetEnvironmentVariable(apiKeyEnvVar);
        if (string.IsNullOrEmpty(apiKey))
        {
            return Result<Ouroboros.Abstractions.Core.IChatCompletionModel, string>.Failure(
                $"API key env var '{apiKeyEnvVar}' not set for OllamaCloud agent '{agentDef.AgentId}'");
        }

        var settings = new ChatRuntimeSettings
        {
            Temperature = agentDef.Temperature,
            MaxTokens = agentDef.MaxTokens
        };

        var cloudModel = new OllamaCloudChatModel(endpoint, apiKey, agentDef.Model, settings);
        return Result<Ouroboros.Abstractions.Core.IChatCompletionModel, string>.Success(cloudModel);
    }
}
