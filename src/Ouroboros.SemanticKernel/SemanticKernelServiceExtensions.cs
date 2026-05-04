// <copyright file="SemanticKernelServiceExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Ouroboros.Abstractions.Chat;
using Ouroboros.Abstractions.Core;
using Ouroboros.Core.Configuration;
using SkQdrantVectorStore = Microsoft.SemanticKernel.Connectors.Qdrant.QdrantVectorStore;
using Ouroboros.SemanticKernel.Filters;
using Ouroboros.SemanticKernel.VectorData;
using Qdrant.Client;
using SkVectorStore = Microsoft.Extensions.VectorData.VectorStore;

namespace Ouroboros.SemanticKernel;

/// <summary>
/// DI extensions for registering Semantic Kernel services in the Ouroboros container.
/// </summary>
public static class SemanticKernelServiceExtensions
{
    /// <summary>
    /// Default vector dimension for embeddings when not explicitly configured.
    /// </summary>
    private const int DefaultVectorDimension = 1536;

    /// <summary>
    /// Registers a Semantic Kernel <see cref="Kernel"/> as a singleton,
    /// backed by the already-registered <see cref="IChatCompletionModel"/>
    /// and optional <see cref="Ouroboros.Tools.ToolRegistry"/>.
    /// Also registers the <see cref="OuroborosAutoFunctionFilter"/> for
    /// observability of SK auto-function-calling.
    /// </summary>
    public static IServiceCollection AddSemanticKernel(this IServiceCollection services)
    {
        return AddSemanticKernel(services, bingApiKey: null);
    }

    /// <summary>
    /// Registers a Semantic Kernel <see cref="Kernel"/> as a singleton,
    /// with optional web search and memory plugins wired automatically.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="bingApiKey">
    /// Optional Bing Web Search API key. When non-null, a <c>WebSearch</c> plugin is
    /// registered on the kernel. Pass <c>null</c> to skip.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSemanticKernel(
        this IServiceCollection services,
        string? bingApiKey)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Auto-function invocation filter (logging + observability)
        services.TryAddSingleton<IAutoFunctionInvocationFilter, OuroborosAutoFunctionFilter>();

        // AgentFactory (depends on Kernel — registered as singleton)
        services.TryAddSingleton(sp =>
        {
            var kernel = sp.GetRequiredService<Kernel>();
            return new AgentFactory(kernel);
        });

        services.TryAddSingleton(sp =>
        {
            // Collect optional additional plugins
            var plugins = new List<KernelPlugin>();

            // Web search plugin (Bing)
            KernelPlugin? webPlugin = PluginFactory.CreateWebSearchPlugin(bingApiKey);
            if (webPlugin is not null)
            {
                plugins.Add(webPlugin);
            }

            // Memory plugin (backed by ISemanticTextMemory if registered)
            ISemanticTextMemory? memory = sp.GetService<ISemanticTextMemory>();
            if (memory is not null)
            {
                plugins.Add(PluginFactory.CreateMemoryPlugin(memory));
            }

            // Phase 266 (ROLE-01): prefer the role-typed IToolRoleClient over raw
            // IChatClient. The role marker carries the same wire-level behavior (it
            // wraps an IChatClient via RoleClientAdapterBase) but lets policy and
            // budgets diverge per-role — see IToolRoleClient.cs for design intent.
            // Falls back to raw IChatClient when the role marker isn't registered
            // (test contexts that don't pull in the ApiHost engine extensions).
            IToolRoleClient? toolRoleClient = sp.GetService<IToolRoleClient>();
            IChatClient? chatClient = (IChatClient?)toolRoleClient ?? sp.GetService<IChatClient>();
            Ouroboros.Tools.ToolRegistry? tools = sp.GetService<Ouroboros.Tools.ToolRegistry>();
            IEnumerable<KernelPlugin>? additionalPlugins = plugins.Count > 0 ? plugins : null;

            ILogger? log = sp.GetService<ILoggerFactory>()?.CreateLogger("Ouroboros.SemanticKernel.Kernel");
            if (chatClient is not null)
            {
                log?.LogInformation(
                    "[SK] Kernel built with chat surface: {Surface} (Phase 266 ROLE-01)",
                    toolRoleClient is not null ? "IToolRoleClient" : "IChatClient");
                return KernelFactory.CreateKernel(chatClient, tools, additionalPlugins);
            }

            // Fall back to IChatCompletionModel
            var model = sp.GetRequiredService<IChatCompletionModel>();
            log?.LogInformation("[SK] Kernel built with chat surface: IChatCompletionModel (legacy)");
            return KernelFactory.CreateKernel(model, tools, additionalPlugins);
        });

        return services;
    }

    /// <summary>
    /// Registers the SK Qdrant <see cref="SkVectorStore"/> and the
    /// <see cref="VectorDataBridge"/> so that Ouroboros code can consume
    /// SK-backed vector stores through <see cref="IAdvancedVectorStore"/>.
    /// <para>
    /// Requires a <see cref="QdrantClient"/> to already be registered in the container.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="collectionName">The default Qdrant collection name for the bridge.</param>
    /// <param name="vectorDimension">The vector dimension (default 1536 for OpenAI ada-002).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSkVectorStore(
        this IServiceCollection services,
        string collectionName = "ouroboros_vectors",
        int vectorDimension = DefaultVectorDimension)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);

        // SK Qdrant VectorStore (wraps QdrantClient)
        services.TryAddSingleton<SkVectorStore>(sp =>
        {
            var qdrantClient = sp.GetRequiredService<QdrantClient>();
            return new SkQdrantVectorStore(qdrantClient, ownsClient: false);
        });

        // Bridge: SK VectorStore -> Ouroboros IAdvancedVectorStore
        services.TryAddSingleton<IAdvancedVectorStore>(sp =>
        {
            var skStore = sp.GetRequiredService<SkVectorStore>();
            return VectorDataBridge.ToOuroboros(skStore, collectionName, vectorDimension);
        });

        return services;
    }

    /// <summary>
    /// Registers an SK <see cref="VectorStoreCollection{TKey, TRecord}"/> for the
    /// expression-patterns collection used by NanoAtom grammar evolution.
    /// <para>
    /// Requires <see cref="AddSkVectorStore"/> to have been called first so that
    /// <see cref="SkVectorStore"/> is available in the container.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="vectorDimension">The vector dimension (default 1536).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSkExpressionPatterns(
        this IServiceCollection services,
        int vectorDimension = DefaultVectorDimension)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(sp =>
        {
            var skStore = sp.GetRequiredService<SkVectorStore>();

            // Resolve the collection name from the registry if available,
            // otherwise fall back to the known default.
            string collectionName = "ouroboros_expression_patterns";
            IQdrantCollectionRegistry? registry = sp.GetService<IQdrantCollectionRegistry>();
            if (registry is not null &&
                registry.TryGetCollectionName(QdrantCollectionRole.ExpressionPatterns, out string? resolved) &&
                !string.IsNullOrWhiteSpace(resolved))
            {
                collectionName = resolved;
            }

            var definition = ExpressionPatternRecord.BuildDefinition(vectorDimension);
            return skStore.GetCollection<string, ExpressionPatternRecord>(collectionName, definition);
        });

        return services;
    }
}
