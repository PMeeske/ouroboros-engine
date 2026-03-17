// <copyright file="KernelFactory.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Ouroboros.Abstractions.Core;
using Ouroboros.Providers.Meai;

namespace Ouroboros.SemanticKernel;

/// <summary>
/// Factory for creating Semantic Kernel <see cref="Kernel"/> instances from Ouroboros providers.
/// Bridges the Ouroboros provider ecosystem with SK's orchestration capabilities.
/// </summary>
public static class KernelFactory
{
    /// <summary>
    /// Creates a <see cref="Kernel"/> backed by the given Ouroboros chat model.
    /// If the model implements <see cref="IChatClientBridge"/>, the native
    /// <see cref="IChatClient"/> is used directly for zero-overhead interop.
    /// </summary>
    /// <param name="model">The Ouroboros chat completion model.</param>
    /// <param name="tools">Optional tool registry to expose as SK plugins.</param>
    /// <returns>A configured <see cref="Kernel"/> instance.</returns>
    public static Kernel CreateKernel(
        IChatCompletionModel model,
        Ouroboros.Tools.ToolRegistry? tools = null)
    {
        return CreateKernel(model, tools, additionalPlugins: null);
    }

    /// <summary>
    /// Creates a <see cref="Kernel"/> backed by the given Ouroboros chat model,
    /// with optional additional plugins (web search, memory, etc.).
    /// </summary>
    /// <param name="model">The Ouroboros chat completion model.</param>
    /// <param name="tools">Optional tool registry to expose as SK plugins.</param>
    /// <param name="additionalPlugins">
    /// Optional extra <see cref="KernelPlugin"/> instances (e.g. from <see cref="PluginFactory"/>).
    /// </param>
    /// <returns>A configured <see cref="Kernel"/> instance.</returns>
    public static Kernel CreateKernel(
        IChatCompletionModel model,
        Ouroboros.Tools.ToolRegistry? tools,
        IEnumerable<KernelPlugin>? additionalPlugins)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model is IChatClientBridge bridge)
        {
            return BuildKernel(bridge.GetChatClient(), tools, additionalPlugins);
        }

        using var adapter = new CompletionModelChatClientAdapter(model);
        return BuildKernel(adapter, tools, additionalPlugins);
    }

    /// <summary>
    /// Creates a <see cref="Kernel"/> from a pre-existing <see cref="IChatClient"/>.
    /// Useful when the MEAI client is already resolved via DI.
    /// </summary>
    public static Kernel CreateKernel(
        IChatClient chatClient,
        Ouroboros.Tools.ToolRegistry? tools = null)
    {
        return CreateKernel(chatClient, tools, additionalPlugins: null);
    }

    /// <summary>
    /// Creates a <see cref="Kernel"/> from a pre-existing <see cref="IChatClient"/>
    /// with optional additional plugins.
    /// </summary>
    public static Kernel CreateKernel(
        IChatClient chatClient,
        Ouroboros.Tools.ToolRegistry? tools,
        IEnumerable<KernelPlugin>? additionalPlugins)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        return BuildKernel(chatClient, tools, additionalPlugins);
    }

    /// <summary>
    /// Creates a <see cref="Kernel"/> from an <see cref="IOuroborosChatClient"/>
    /// (which is already an IChatClient -- zero adapter overhead).
    /// </summary>
    public static Kernel CreateKernel(
        IOuroborosChatClient client,
        Ouroboros.Tools.ToolRegistry? tools = null)
    {
        return CreateKernel(client, tools, additionalPlugins: null);
    }

    /// <summary>
    /// Creates a <see cref="Kernel"/> from an <see cref="IOuroborosChatClient"/>
    /// with optional additional plugins.
    /// </summary>
    public static Kernel CreateKernel(
        IOuroborosChatClient client,
        Ouroboros.Tools.ToolRegistry? tools,
        IEnumerable<KernelPlugin>? additionalPlugins)
    {
        ArgumentNullException.ThrowIfNull(client);
        return BuildKernel(client, tools, additionalPlugins);
    }

    private static Kernel BuildKernel(
        IChatClient chatClient,
        Ouroboros.Tools.ToolRegistry? tools,
        IEnumerable<KernelPlugin>? additionalPlugins = null)
    {
        var builder = Kernel.CreateBuilder();

        // Register IChatClient via SK's MEAI bridge extension, which handles both
        // the singleton registration and the IChatClientBuilder pipeline internally.
        builder.Services.AddChatClient(chatClient);

        if (tools is { Count: > 0 })
        {
            KernelPlugin plugin = ToolRegistryPluginBridge.ToKernelPlugin(tools);
            builder.Plugins.Add(plugin);
        }

        if (additionalPlugins is not null)
        {
            foreach (KernelPlugin plugin in additionalPlugins)
            {
                builder.Plugins.Add(plugin);
            }
        }

        return builder.Build();
    }
}
