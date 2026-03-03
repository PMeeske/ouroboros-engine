// <copyright file="LangChainServiceExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using LangChain.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ouroboros.LangChainBridge;

/// <summary>
/// DI extensions for registering LangChain integration with Ouroboros providers.
/// </summary>
public static class LangChainServiceExtensions
{
    /// <summary>
    /// Registers a LangChain <see cref="IChatModel"/> backed by the MEAI
    /// <see cref="IChatClient"/> already in the container.
    /// Call after <c>AddMeaiChatClient()</c> to ensure the IChatClient is available.
    /// </summary>
    public static IServiceCollection AddLangChainIntegration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IChatModel>(sp =>
        {
            var client = sp.GetRequiredService<IChatClient>();
            return new ChatClientChatModel(client);
        });

        return services;
    }
}
