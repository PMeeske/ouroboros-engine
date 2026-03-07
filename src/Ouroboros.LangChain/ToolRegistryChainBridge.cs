// <copyright file="ToolRegistryChainBridge.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using LangChain.Providers;
using Ouroboros.Tools;

namespace Ouroboros.LangChainBridge;

/// <summary>
/// Bridges <see cref="ToolRegistry"/> tools to LangChain's global tool system
/// via <see cref="IChatModel.AddGlobalTools"/>.
/// </summary>
public static class ToolRegistryChainBridge
{
    /// <summary>
    /// Registers all <see cref="ITool"/> entries from the <see cref="ToolRegistry"/>
    /// as global tools on a LangChain <see cref="IChatModel"/>.
    /// </summary>
    /// <param name="chatModel">The LangChain chat model to register tools on.</param>
    /// <param name="registry">The Ouroboros tool registry.</param>
    /// <param name="filter">Optional tool name filter; only matching tools are registered.</param>
    public static void RegisterTools(
        IChatModel chatModel,
        ToolRegistry registry,
        Func<string, bool>? filter = null)
    {
        ArgumentNullException.ThrowIfNull(chatModel);
        ArgumentNullException.ThrowIfNull(registry);

        var toolCallbacks = new Dictionary<string, Func<string, CancellationToken, Task<string>>>();

        foreach (var tool in registry.All)
        {
            if (filter != null && !filter(tool.Name))
                continue;

            // Capture the tool reference for the closure
            var captured = tool;
            toolCallbacks[captured.Name] = async (input, ct) =>
            {
                var result = await captured.InvokeAsync(input, ct).ConfigureAwait(false);
                return result.Match(
                    success => success,
                    error => $"Error: {error}");
            };
        }

        if (toolCallbacks.Count > 0)
        {
            // LangChain's AddGlobalTools expects ICollection<CSharpToJsonSchema.Tool>
            // and a callbacks dictionary. We pass an empty tool definitions list since
            // our tools are invoked via the callback dictionary keyed by name.
            chatModel.AddGlobalTools(
                Array.Empty<CSharpToJsonSchema.Tool>(),
                toolCallbacks);
        }
    }
}
