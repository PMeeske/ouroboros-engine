// <copyright file="McpServerExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ouroboros.McpServer;

/// <summary>
/// Dependency injection extensions for registering the Ouroboros MCP server.
/// </summary>
public static class McpServerExtensions
{
    /// <summary>
    /// Adds the Ouroboros MCP server to the service collection,
    /// exposing <see cref="ToolRegistry"/> tools via the Model Context Protocol.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Optional callback to configure <see cref="McpServerOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOuroborosMcpServer(
        this IServiceCollection services,
        Action<McpServerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new McpServerOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton(sp =>
        {
            var registry = sp.GetRequiredService<ToolRegistry>();
            return new OuroborosMcpServer(registry, options);
        });

        return services;
    }
}
