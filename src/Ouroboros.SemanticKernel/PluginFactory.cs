// <copyright file="PluginFactory.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Memory;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;

namespace Ouroboros.SemanticKernel;

/// <summary>
/// Factory for creating Semantic Kernel plugins from external service configuration.
/// Encapsulates the conditional wiring of optional SK plugins (web search, memory recall)
/// so that callers do not need to depend on the individual plugin packages directly.
/// </summary>
public static class PluginFactory
{
    /// <summary>
    /// Creates a <see cref="WebSearchEnginePlugin"/> backed by the Bing connector
    /// if an API key is provided. Returns <c>null</c> when no key is available,
    /// allowing callers to skip registration gracefully.
    /// </summary>
    /// <param name="bingApiKey">
    /// The Bing Web Search API key. Pass <c>null</c> or empty to indicate the plugin
    /// should not be created.
    /// </param>
    /// <returns>A <see cref="KernelPlugin"/> wrapping web search, or <c>null</c>.</returns>
    public static KernelPlugin? CreateWebSearchPlugin(string? bingApiKey)
    {
        if (string.IsNullOrWhiteSpace(bingApiKey))
        {
            return null;
        }

        var connector = new BingConnector(bingApiKey);
        var plugin = new WebSearchEnginePlugin(connector);
        return KernelPluginFactory.CreateFromObject(plugin, "WebSearch");
    }

    /// <summary>
    /// Creates a <see cref="TextMemoryPlugin"/> backed by the given
    /// <see cref="ISemanticTextMemory"/> instance (typically Qdrant-backed).
    /// </summary>
    /// <param name="memory">The semantic text memory store.</param>
    /// <returns>A <see cref="KernelPlugin"/> wrapping memory recall and save.</returns>
    public static KernelPlugin CreateMemoryPlugin(ISemanticTextMemory memory)
    {
        ArgumentNullException.ThrowIfNull(memory);

        var plugin = new TextMemoryPlugin(memory);
        return KernelPluginFactory.CreateFromObject(plugin, "Memory");
    }
}
