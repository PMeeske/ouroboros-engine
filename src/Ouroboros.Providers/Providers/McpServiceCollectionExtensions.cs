// <copyright file="McpServiceCollectionExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Providers.Docker;
using Ouroboros.Providers.DuckDuckGo;
using Ouroboros.Providers.Firecrawl;
using Ouroboros.Providers.Kubernetes;

namespace Ouroboros.Providers;

/// <summary>
/// Dependency injection extensions for MCP client registrations.
/// </summary>
public static class McpServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Kubernetes MCP client with the specified options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configurator for options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKubernetesMcpClient(
        this IServiceCollection services,
        Action<KubernetesMcpClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new KubernetesMcpClientOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IKubernetesMcpClient>(sp =>
            new KubernetesMcpClient(sp.GetRequiredService<KubernetesMcpClientOptions>()));

        return services;
    }

    /// <summary>
    /// Registers the Docker MCP client with the specified options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configurator for options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDockerMcpClient(
        this IServiceCollection services,
        Action<DockerMcpClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new DockerMcpClientOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IDockerMcpClient>(sp =>
            new DockerMcpClient(sp.GetRequiredService<DockerMcpClientOptions>()));

        return services;
    }

    /// <summary>
    /// Registers the Firecrawl MCP client with the specified options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configurator for options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFirecrawlMcpClient(
        this IServiceCollection services,
        Action<FirecrawlMcpClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new FirecrawlMcpClientOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IFirecrawlMcpClient>(sp =>
            new FirecrawlMcpClient(sp.GetRequiredService<FirecrawlMcpClientOptions>()));

        return services;
    }

    /// <summary>
    /// Registers the DuckDuckGo MCP client with the specified options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configurator for options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDuckDuckGoMcpClient(
        this IServiceCollection services,
        Action<DuckDuckGoMcpClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new DuckDuckGoMcpClientOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IDuckDuckGoMcpClient>(sp =>
            new DuckDuckGoMcpClient(sp.GetRequiredService<DuckDuckGoMcpClientOptions>()));

        return services;
    }

    /// <summary>
    /// Registers all available MCP clients (Kubernetes, Docker, Firecrawl, DuckDuckGo).
    /// Clients with missing credentials will still be registered but will fail at runtime
    /// if used without proper configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAllMcpClients(this IServiceCollection services)
    {
        return services
            .AddKubernetesMcpClient()
            .AddDockerMcpClient()
            .AddFirecrawlMcpClient()
            .AddDuckDuckGoMcpClient();
    }
}
