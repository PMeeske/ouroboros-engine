// <copyright file="SemanticKernelServiceExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Ouroboros.Abstractions.Core;
using SkQdrantVectorStore = Microsoft.SemanticKernel.Connectors.Qdrant.QdrantVectorStore;
using Ouroboros.Domain.Vectors;
using Ouroboros.Providers.Meai;
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
        ArgumentNullException.ThrowIfNull(services);

        // ── Auto-function invocation filter (logging + observability) ────────
        services.TryAddSingleton<IAutoFunctionInvocationFilter, OuroborosAutoFunctionFilter>();

        services.TryAddSingleton(sp =>
        {
            // Prefer IChatClient if already registered (e.g. via AddMeaiChatClient)
            IChatClient? chatClient = sp.GetService<IChatClient>();
            Ouroboros.Tools.ToolRegistry? tools = sp.GetService<Ouroboros.Tools.ToolRegistry>();

            if (chatClient is not null)
            {
                return KernelFactory.CreateKernel(chatClient, tools);
            }

            // Fall back to IChatCompletionModel
            var model = sp.GetRequiredService<IChatCompletionModel>();
            return KernelFactory.CreateKernel(model, tools);
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

        // ── SK Qdrant VectorStore (wraps QdrantClient) ───────────────────────
        services.TryAddSingleton<SkVectorStore>(sp =>
        {
            var qdrantClient = sp.GetRequiredService<QdrantClient>();
            return new SkQdrantVectorStore(qdrantClient, ownsClient: false);
        });

        // ── Bridge: SK VectorStore → Ouroboros IAdvancedVectorStore ──────────
        services.TryAddSingleton<IAdvancedVectorStore>(sp =>
        {
            var skStore = sp.GetRequiredService<SkVectorStore>();
            return VectorDataBridge.ToOuroboros(skStore, collectionName, vectorDimension);
        });

        return services;
    }
}
