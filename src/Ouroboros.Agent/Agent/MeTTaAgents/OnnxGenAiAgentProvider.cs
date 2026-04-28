// <copyright file="OnnxGenAiAgentProvider.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Agent.MeTTaAgents;
using Ouroboros.Providers;
using Ouroboros.Providers.Configuration;

namespace Ouroboros.Agent.MeTTaAgents;

/// <summary>
/// Creates ONNX Runtime GenAI-backed agents from MeTTa definitions.
/// Supports local ONNX-exported LLMs (Hermes-3, Phi-3, Llama, Mistral, etc.)
/// with DirectML or CPU execution.
/// </summary>
/// <remarks>
/// <para>
/// Register this provider in <see cref="MeTTaAgentRuntime"/> alongside
/// <see cref="OllamaAgentProvider"/> and <see cref="MockAgentProvider"/>.
/// Agent definitions must set <c>Provider="OnnxGenAI"</c> and <c>Model</cc>
/// to the local model directory path (e.g. 
/// <c>./models/hermes-3-llama-3.1-8b-int4</c>).
/// </para>
/// <para>
/// Environment variables:
///   <item><c>OUROBOROS_ONNX_MODEL_PATH</c> – fallback base directory for model lookup</item>
///   <item><c>HERMES_ONNX_MODEL</c> – specific Hermes model dir name</item>
///   <item><c>ONNX_USE_GPU</c> – <c>1</c> to force GPU (DirectML), <c>0</c> for CPU</item>
/// </para>
/// </remarks>
public sealed class OnnxGenAiAgentProvider : IAgentProviderFactory
{
    /// <summary>Provider name this factory handles.</summary>
    public const string ProviderName = "OnnxGenAI";

    /// <summary>Short alias / legacy name for OpenVINO users.</summary>
    public const string ProviderAlias = "Onnx";

    private readonly string _defaultModelBasePath;
    private readonly OnnxRuntimeSettings _defaultSettings;
    private readonly IOnnxExecutionProvider? _fallbackProvider;

    /// <summary>
    /// Creates a new ONNX GenAI agent provider.
    /// </summary>
    /// <param name="defaultModelBasePath">Root directory containing exported ONNX model folders.</parameter>
    /// <param name="defaultSettings">Default generation settings when agent definition omits them.</parameter>
    public OnnxGenAiAgentProvider(
        string? defaultModelBasePath = null,
        OnnxRuntimeSettings? defaultSettings = null)
    {
        _defaultModelBasePath = defaultModelBasePath
            ?? Environment.GetEnvironmentVariable("OUROBOROS_ONNX_MODEL_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "models");

        _defaultSettings = defaultSettings ?? OnnxRuntimeSettings.Default;
        _fallbackProvider = ResolveExecutionProvider();
    }

    // ═══════════════════ IAgentProviderFactory ═══════════════════

    /// <inheritdoc/>
    public bool CanHandle(string providerName)
        => providerName.Equals(ProviderName, StringComparison.OrdinalIgnoreCase)
        || providerName.Equals(ProviderAlias, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    /// <exception cref="DirectoryNotFoundException">When the resolved model directory does not exist.</exception>
    public Task<Result<Ouroboros.Abstractions.Core.IChatCompletionModel, string>> CreateModelAsync(
        MeTTaAgentDef agentDef, CancellationToken ct = default)
    {
        try
        {
            // Resolve absolute model path
            string modelPath = ResolveModelPath(agentDef.Model, agentDef.Endpoint);
            if (!Directory.Exists(modelPath))
            {
                return Task.FromResult(
                    Result<Ouroboros.Abstractions.Core.IChatCompletionModel, string>.Failure(
                        $"ONNX model directory not found: '{modelPath}'. " +
                        "Export it via: optimum-cli export onnx --model NousResearch/Hermes-3-Llama-3.1-8B " +
                        $"--dtype int4 --task text-generation-with-past {modelPath}"));
            }

            // Build settings from agent definition, falling back to defaults
            OnnxRuntimeSettings settings = BuildSettings(agentDef);

            var model = new OnnxGenAiChatModel(
                modelPath,
                settings,
                executionProvider: _fallbackProvider,
                costTracker: new LlmCostTracker()); // One tracker per agent instance

            return Task.FromResult(
                Result<Ouroboros.Abstractions.Core.IChatCompletionModel, string>.Success(model));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult(
                Result<Ouroboros.Abstractions.Core.IChatCompletionModel, string>.Failure(
                    $"Failed to create ONNX GenAI agent '{agentDef.AgentId}': {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<ProviderHealthStatus, string>> HealthCheckAsync(CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            string modelPath = _defaultModelBasePath;
            if (!Directory.Exists(modelPath))
            {
                sw.Stop();
                return Task.FromResult(
                    Result<ProviderHealthStatus, string>.Success(
                        new ProviderHealthStatus("OnnxGenAI", false, sw.ElapsedMilliseconds,
                            $"Default model base path does not exist: {modelPath}")));
            }

            // Probe: attempt to list model files (lightweight check before instantiating Model)
            string[]? modelFiles = Directory.GetFiles(modelPath, "*.onnx", SearchOption.AllDirectories);
            bool hasModel = modelFiles.Length > 0;
            string? message = hasModel
                ? null
                : $"No .onnx files found under {modelPath}. Models may need export.";

            sw.Stop();
            return Task.FromResult(
                Result<ProviderHealthStatus, string>.Success(
                    new ProviderHealthStatus("OnnxGenAI", hasModel, sw.ElapsedMilliseconds, message)));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            return Task.FromResult(
                Result<ProviderHealthStatus, string>.Success(
                    new ProviderHealthStatus("OnnxGenAI", false, sw.ElapsedMilliseconds, ex.Message)));
        }
    }

    // ═══════════════════ private helpers ═══════════════════

    /// <summary>
    /// Resolves an absolute path for the model directory.
    /// </summary>
    private string ResolveModelPath(string modelRef, string? endpointOverride)
    {
        // If endpoint is a directory, use it as the base path
        string basePath = !string.IsNullOrWhiteSpace(endpointOverride) && Directory.Exists(endpointOverride)
            ? endpointOverride
            : _defaultModelBasePath;

        // If modelRef is already an absolute path, use it directly
        if (Path.IsPathFullyQualified(modelRef) && Directory.Exists(modelRef))
            return modelRef;

        // Otherwise, join base path with model ref (folder name)
        return Path.Combine(basePath, modelRef);
    }

    /// <summary>
    /// Merges agent-specific overrides into the provider-level defaults.
    /// </summary>
    private OnnxRuntimeSettings BuildSettings(MeTTaAgentDef agentDef)
    {
        return _defaultSettings with
        {
            Temperature = agentDef.Temperature > 0 ? agentDef.Temperature : _defaultSettings.Temperature,
            TopP = _defaultSettings.TopP,
            TopK = _defaultSettings.TopK,
            MaxLength = agentDef.MaxTokens > 0 ? agentDef.MaxTokens : _defaultSettings.MaxLength,
            RepetitionPenalty = _defaultSettings.RepetitionPenalty,
        };
    }

    /// <summary>
    /// Reads <c>ONNX_USE_GPU</c> env var to pick execution provider.
    /// </summary>
    private static IOnnxExecutionProvider? ResolveExecutionProvider()
    {
        string? gpuEnv = Environment.GetEnvironmentVariable("ONNX_USE_GPU");
        bool useGpu = string.IsNullOrWhiteSpace(gpuEnv)
            ? true   // default: try GPU
            : gpuEnv.Equals("1", StringComparison.OrdinalIgnoreCase)
              || gpuEnv.Equals("true", StringComparison.OrdinalIgnoreCase);

        return useGpu ? OnnxExecutionProviderFactory.CreateBest() : OnnxExecutionProviderFactory.Create(false);
    }
}

/// <summary>
/// Extension methods for wiring the ONNX GenAI provider into agent runtimes.
/// </summary>
public static class OnnxGenAiAgentProviderExtensions
{
    /// <summary>
    /// Registers <see cref="OnnxGenAiAgentProvider"/> into a collection of agent provider factories.
    /// </summary>
    public static System.Collections.Generic.List<IAgentProviderFactory> AddOnnxGenAI(
        this System.Collections.Generic.List<IAgentProviderFactory> factories,
        string? defaultModelBasePath = null,
        OnnxRuntimeSettings? settings = null)
    {
        factories.Add(new OnnxGenAiAgentProvider(defaultModelBasePath, settings));
        return factories;
    }
}
