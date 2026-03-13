// <copyright file="OllamaAgentProvider.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using OllamaSharp;
using Ouroboros.Providers;
using Ouroboros.Providers.Configuration;

namespace Ouroboros.Agent.MeTTaAgents;

/// <summary>
/// Creates Ollama-backed agents from MeTTa definitions.
/// Supports both local Ollama and Ollama Cloud endpoints.
/// </summary>
public sealed class OllamaAgentProvider : IAgentProviderFactory
{
    private readonly string _defaultEndpoint;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, OllamaApiClient> _providers = new();

    /// <summary>
    /// Creates a new Ollama agent provider.
    /// </summary>
    /// <param name="defaultEndpoint">Default Ollama endpoint URL.</param>
    public OllamaAgentProvider(string defaultEndpoint = DefaultEndpoints.Ollama)
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

            // Local Ollama via OllamaSharp - cache client per endpoint
            var client = _providers.GetOrAdd(endpoint, ep => new OllamaApiClient(new Uri(ep)));
            var adapter = new OllamaChatAdapter(client, agentDef.Model);
            return Task.FromResult(
                Result<Ouroboros.Abstractions.Core.IChatCompletionModel, string>.Success(adapter));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
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
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5), BaseAddress = new Uri(_defaultEndpoint) };
            using var client = new OllamaApiClient(http);
            await client.ListLocalModelsAsync(ct).ConfigureAwait(false);
            sw.Stop();
            return Result<ProviderHealthStatus, string>.Success(
                new ProviderHealthStatus("Ollama", true, sw.ElapsedMilliseconds));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
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
