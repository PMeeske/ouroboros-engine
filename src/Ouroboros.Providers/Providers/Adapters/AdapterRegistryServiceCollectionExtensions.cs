// <copyright file="AdapterRegistryServiceCollectionExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Ouroboros.Providers.Adapters;

/// <summary>
/// Dependency injection helpers for registering the LoRA adapter registry.
/// </summary>
public static class AdapterRegistryServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="OgaAdapterRegistry"/> as the singleton <see cref="IAdapterRegistry"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="adapterRootPath">Absolute path to the directory containing <c>*.adapter.json</c> manifests.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Phase A scaffold — registers the registry and triggers no model loading. Call
    /// <see cref="IAdapterRegistry.LoadFromDiskAsync"/> from a hosted service or startup
    /// hook to populate the index.
    /// </remarks>
    public static IServiceCollection AddOgaAdapterRegistry(
        this IServiceCollection services,
        string adapterRootPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterRootPath);

        services.TryAddSingleton<IAdapterRegistry>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<OgaAdapterRegistry>>();
            return new OgaAdapterRegistry(adapterRootPath, logger);
        });

        return services;
    }
}
