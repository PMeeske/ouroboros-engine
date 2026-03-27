// <copyright file="ServiceCollectionExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

#pragma warning disable CS0618 // Obsolete Qdrant types — intentional DI registration of direct-client services

namespace Ouroboros.Providers;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OllamaSharp;
using OllamaSharp.Models;
using Ouroboros.Core.Configuration;
using Ouroboros.Domain.Autonomous;
using Ouroboros.Domain.Vectors;
using Ouroboros.Providers.Meai;
using Ouroboros.Providers.SpeechToText;
using Ouroboros.Providers.TextToSpeech;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;

/// <summary>
/// Dependency injection helpers for registering chat and embedding models.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register an interchangeable chat + embedding stack that prefers remote OpenAI-compatible
    /// endpoints when configured, falling back to local Ollama with deterministic embeddings otherwise.
    /// </summary>
    /// <returns></returns>
    public static IServiceCollection AddInterchangeableLlm(this IServiceCollection services, string? model = null, string? embed = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        model = string.IsNullOrWhiteSpace(model) ? "llama3" : model;
        embed = string.IsNullOrWhiteSpace(embed) ? "nomic-embed-text" : embed;

        services.TryAddSingleton<Ouroboros.Abstractions.Core.IChatCompletionModel>(sp =>
        {
            (string? endpoint, string? apiKey, ChatEndpointType endpointType) = ChatConfig.Resolve();
            if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
            {
                try
                {
                    return endpointType switch
                    {
                        ChatEndpointType.OllamaCloud => new OllamaCloudChatModel(endpoint!, apiKey!, model!),
                        ChatEndpointType.OpenAiCompatible => new HttpOpenAiCompatibleChatModel(endpoint!, apiKey!, model!),
                        ChatEndpointType.Auto => new HttpOpenAiCompatibleChatModel(endpoint!, apiKey!, model!),
                        _ => new HttpOpenAiCompatibleChatModel(endpoint!, apiKey!, model!),
                    };
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Ignore and fall back to local Ollama below.
                }
            }

            var ollamaClient = sp.GetService<OllamaApiClient>()
                ?? new OllamaApiClient(new Uri(Configuration.DefaultEndpoints.Ollama), model!);
            var adapter = new OllamaChatAdapter(ollamaClient, model!);
            try
            {
                (RequestOptions? preset, string? keepAlive) = GetPresetForModel(model!);
                adapter.Options = preset;
                adapter.KeepAlive = keepAlive;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best-effort mapping; if detection fails we keep provider defaults.
            }

            return adapter;
        });

        services.TryAddSingleton<IEmbeddingModel>(sp =>
        {
            var qdrant = sp.GetService<QdrantClient>();
            var logger = sp.GetService<ILogger<TensorEmbeddingModel>>();
            var onnxPath = Environment.GetEnvironmentVariable("OUROBOROS_EMBEDDING_ONNX_MODEL");
            return new TensorEmbeddingModel(qdrant, logger, onnxPath);
        });

        services.TryAddSingleton<ToolRegistry>();
        services.TryAddSingleton(sp =>
        {
            ToolRegistry registry = sp.GetRequiredService<ToolRegistry>();
            Ouroboros.Abstractions.Core.IChatCompletionModel chat = sp.GetRequiredService<Ouroboros.Abstractions.Core.IChatCompletionModel>();
            return new ToolAwareChatModel(chat, registry);
        });

        return services;
    }

    /// <summary>
    /// Registers a MEAI <see cref="IChatClient"/> in the container.
    /// If the resolved <see cref="Ouroboros.Abstractions.Core.IChatCompletionModel"/>
    /// implements <see cref="Ouroboros.Abstractions.Core.IChatClientBridge"/>
    /// (e.g. <see cref="OllamaChatAdapter"/>), the native client is returned directly
    /// for zero-overhead interop. Otherwise, a <see cref="CompletionModelChatClientAdapter"/> wraps the model.
    /// </summary>
    public static IServiceCollection AddMeaiChatClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IChatClient>(sp =>
        {
            var model = sp.GetRequiredService<Ouroboros.Abstractions.Core.IChatCompletionModel>();
            return model is Ouroboros.Abstractions.Core.IChatClientBridge bridge
                ? bridge.GetChatClient()
                : new CompletionModelChatClientAdapter(model);
        });

        services.TryAddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var model = sp.GetRequiredService<IEmbeddingModel>();
            return model is Ouroboros.Abstractions.Core.IEmbeddingGeneratorBridge bridge
                ? bridge.GetEmbeddingGenerator()
                : new EmbeddingModelGeneratorAdapter(model);
        });

        // Reverse bridge: when IEmbeddingGenerator is registered externally
        // (e.g. via Semantic Kernel or MEAI) but no IEmbeddingModel exists,
        // wrap the generator so legacy consumers still resolve.
        services.TryAddSingleton<IEmbeddingModel>(sp =>
        {
            var generator = sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
            // Unwrap if the generator is already our forward adapter (avoid double-wrapping)
            if (generator.GetService(typeof(IEmbeddingModel)) is IEmbeddingModel inner)
                return inner;
            return new EmbeddingGeneratorModelAdapter(generator);
        });

        services.TryAddSingleton<Ouroboros.Abstractions.Core.IOuroborosChatClient>(sp =>
        {
            var model = sp.GetRequiredService<Ouroboros.Abstractions.Core.IChatCompletionModel>();
            if (model is Ouroboros.Abstractions.Core.IOuroborosChatClient ouroClient)
                return ouroClient;
            throw new InvalidOperationException(
                $"The resolved IChatCompletionModel ({model.GetType().Name}) does not implement IOuroborosChatClient. " +
                "Migrate the provider to IOuroborosChatClient or use IChatClient via AddMeaiChatClient() instead.");
        });

        return services;
    }

    /// <summary>
    /// Resolves the appropriate <see cref="RequestOptions"/> preset and KeepAlive duration
    /// for a given Ollama model name. Returns <c>(null, null)</c> when no preset is recognized.
    /// </summary>
    private static (RequestOptions? Preset, string? KeepAlive) GetPresetForModel(string model)
    {
        string n = (model ?? string.Empty).ToLowerInvariant();

        if (n.StartsWith("deepseek-coder:33b"))
            return (OllamaPresets.DeepSeekCoder33B, OllamaPresets.DeepSeekCoder33BKeepAlive);

        if (n.StartsWith("llama3"))
            return (OllamaPresets.Llama3General, OllamaPresets.Llama3GeneralKeepAlive);

        if (n.StartsWith("deepseek-r1:32") || n.Contains("32b"))
            return (OllamaPresets.DeepSeekR1_32B_Reason, OllamaPresets.DeepSeekR1_32B_ReasonKeepAlive);

        if (n.StartsWith("deepseek-r1:14") || n.Contains("14b"))
            return (OllamaPresets.DeepSeekR1_14B_Reason, OllamaPresets.DeepSeekR1_14B_ReasonKeepAlive);

        if (n.Contains("mistral") && (n.Contains("7b") || !n.Contains("large")))
            return (OllamaPresets.Mistral7BGeneral, OllamaPresets.Mistral7BGeneralKeepAlive);

        if (n.StartsWith("qwen2.5") || n.Contains("qwen"))
            return (OllamaPresets.Qwen25_7B_General, OllamaPresets.Qwen25_7B_GeneralKeepAlive);

        if (n.StartsWith("phi3") || n.Contains("phi-3"))
            return (OllamaPresets.Phi3MiniGeneral, OllamaPresets.Phi3MiniGeneralKeepAlive);

        return (null, null);
    }

    /// <summary>
    /// Registers centralized Qdrant infrastructure as a cross-cutting concern:
    /// <list type="bullet">
    ///   <item><see cref="QdrantSettings"/> bound from <c>Ouroboros:Qdrant</c> in appsettings</item>
    ///   <item>Singleton <see cref="IQdrantClient"/> (<see cref="QdrantClient"/>)</item>
    ///   <item>Singleton <see cref="IQdrantCollectionRegistry"/> for role-based collection resolution</item>
    /// </list>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration root.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddQdrant(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Bind settings from appsettings "Ouroboros:Qdrant"
        services.Configure<QdrantSettings>(
            configuration.GetSection(QdrantSettings.SectionPath));

        // Bind collection overrides from "Ouroboros:Qdrant:Collections"
        services.Configure<QdrantCollectionOverrides>(
            configuration.GetSection($"{QdrantSettings.SectionPath}:Collections"));

        // Register singleton QdrantClient as IQdrantClient
        services.TryAddSingleton<IQdrantClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<QdrantSettings>>().Value;
            return new QdrantClient(
                settings.Host,
                settings.GrpcPort,
                settings.UseHttps,
                apiKey: settings.ApiKey);
        });

        // Also register concrete QdrantClient (same instance) for backward compat
        services.TryAddSingleton(sp =>
            (QdrantClient)sp.GetRequiredService<IQdrantClient>());

        // Register collection registry
        services.TryAddSingleton<IQdrantCollectionRegistry>(sp =>
        {
            var client = sp.GetRequiredService<QdrantClient>();
            var overrides = sp.GetService<IOptions<QdrantCollectionOverrides>>();
            var logger = sp.GetService<ILogger<QdrantCollectionRegistry>>();
            return overrides != null
                ? new QdrantCollectionRegistry(client, overrides, logger)
                : new QdrantCollectionRegistry(client, logger);
        });

        // Register QdrantSettings as a concrete singleton for direct injection
        services.TryAddSingleton(sp =>
            sp.GetRequiredService<IOptions<QdrantSettings>>().Value);

        return services;
    }

    /// <summary>
    /// Registers Qdrant-consuming services that can be pre-resolved at startup.
    /// Services needing runtime deps (embedding, MeTTa) are created by subsystems using
    /// <see cref="QdrantClient"/> and <see cref="IQdrantCollectionRegistry"/> from DI.
    /// </summary>
    public static IServiceCollection AddQdrantServices(
        this IServiceCollection services)
    {
        // QdrantNeuralMemory — uses gRPC client + registry
        services.TryAddSingleton(sp =>
        {
            var client = sp.GetRequiredService<QdrantClient>();
            var registry = sp.GetRequiredService<IQdrantCollectionRegistry>();
            var settings = sp.GetRequiredService<QdrantSettings>();
            return new QdrantNeuralMemory(client, registry, settings);
        });

        // QdrantCollectionAdmin — needs gRPC client + registry
        services.TryAddSingleton(sp =>
        {
            var client = sp.GetRequiredService<QdrantClient>();
            var registry = sp.GetRequiredService<IQdrantCollectionRegistry>();
            return new QdrantCollectionAdmin(client, registry);
        });

        return services;
    }

    /// <summary>
    /// Register vector store based on configuration.
    /// Supports InMemory (default), Qdrant, and extensible to other backends.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Optional explicit configuration. If null, reads from IOptions.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVectorStore(this IServiceCollection services, VectorStoreConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register the factory — use DI-provided Qdrant client when available
        services.TryAddSingleton<VectorStoreFactory>(sp =>
        {
            var config = configuration ?? sp.GetService<IOptions<VectorStoreConfiguration>>()?.Value ?? new VectorStoreConfiguration();
            var qdrantClient = sp.GetService<QdrantClient>();
            var registry = sp.GetService<IQdrantCollectionRegistry>();
            var logger = sp.GetService<ILogger<VectorStoreFactory>>();
            return new VectorStoreFactory(config, qdrantClient, registry, logger);
        });

        // Register IVectorStore using the factory
        services.TryAddSingleton<IVectorStore>(sp =>
        {
            var factory = sp.GetRequiredService<VectorStoreFactory>();
            return factory.Create();
        });

        return services;
    }

    /// <summary>
    /// Register vector store with explicit type selection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="storeType">Type of store: "InMemory", "Qdrant".</param>
    /// <param name="connectionString">Connection string for external stores.</param>
    /// <param name="collectionName">Collection/index name.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVectorStore(
        this IServiceCollection services,
        string storeType,
        string? connectionString = null,
        string collectionName = "pipeline_vectors")
    {
        var config = new VectorStoreConfiguration
        {
            Type = storeType,
            ConnectionString = connectionString,
            DefaultCollection = collectionName
        };

        return services.AddVectorStore(config);
    }

    /// <summary>
    /// Register OpenAI Whisper speech-to-text service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">OpenAI API key. If null, reads from OPENAI_API_KEY environment variable.</param>
    /// <param name="model">Whisper model to use (default: whisper-1).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWhisperSpeechToText(
        this IServiceCollection services,
        string? apiKey = null,
        string model = "whisper-1")
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ISpeechToTextService>(sp =>
        {
            var key = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? throw new InvalidOperationException("OpenAI API key required. Set OPENAI_API_KEY or pass apiKey parameter.");
            return new WhisperSpeechToTextService(key, model: model);
        });

        return services;
    }

    /// <summary>
    /// Register local Whisper speech-to-text service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="modelSize">Model size: tiny, base, small, medium, large.</param>
    /// <param name="whisperPath">Optional path to whisper executable.</param>
    /// <param name="modelPath">Optional path to model file.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalWhisperSpeechToText(
        this IServiceCollection services,
        string modelSize = "base",
        string? whisperPath = null,
        string? modelPath = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ISpeechToTextService>(sp =>
            new LocalWhisperService(whisperPath, modelPath, modelSize));

        return services;
    }

    /// <summary>
    /// Register a custom speech-to-text service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="service">The speech-to-text service instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSpeechToText(
        this IServiceCollection services,
        ISpeechToTextService service)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(service);

        services.TryAddSingleton(service);
        return services;
    }

    /// <summary>
    /// Register OpenAI text-to-speech service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">OpenAI API key. If null, reads from OPENAI_API_KEY environment variable.</param>
    /// <param name="model">TTS model to use: "tts-1" (faster) or "tts-1-hd" (higher quality).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOpenAiTextToSpeech(
        this IServiceCollection services,
        string? apiKey = null,
        string model = "tts-1")
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ITextToSpeechService>(sp =>
        {
            string key = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? throw new InvalidOperationException("OpenAI API key required. Set OPENAI_API_KEY or pass apiKey parameter.");
            return new OpenAiTextToSpeechService(key, model: model);
        });

        return services;
    }

    /// <summary>
    /// Register a custom text-to-speech service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="service">The text-to-speech service instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTextToSpeech(
        this IServiceCollection services,
        ITextToSpeechService service)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(service);

        services.TryAddSingleton(service);
        return services;
    }

    /// <summary>
    /// Register both speech-to-text and text-to-speech services for bidirectional audio support.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">OpenAI API key. If null, reads from OPENAI_API_KEY environment variable.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBidirectionalSpeech(
        this IServiceCollection services,
        string? apiKey = null)
    {
        return services
            .AddWhisperSpeechToText(apiKey)
            .AddOpenAiTextToSpeech(apiKey);
    }
}
