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
    /// Registers <see cref="OgaAdapterRegistry"/> as the singleton <see cref="IAdapterRegistry"/>
    /// without an underlying model session. Activate calls will return a Failure indicating
    /// no session is bound — useful for staged DI wiring and tests.
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
        => AddOgaAdapterRegistry(services, adapterRootPath, modelPath: null);

    /// <summary>
    /// Registers <see cref="OgaAdapterRegistry"/> as the singleton <see cref="IAdapterRegistry"/>
    /// and, when <paramref name="modelPath"/> is non-null and non-whitespace, also registers
    /// a singleton <see cref="OgaModelSession"/> bound to the registry so Activate/Deactivate
    /// are fully wired against the underlying ORT-GenAI Model + Adapters API.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="adapterRootPath">Absolute path to the directory containing <c>*.adapter.json</c> manifests.</param>
    /// <param name="modelPath">
    /// Optional absolute path to the OGA model directory (containing <c>model.onnx</c>,
    /// <c>model.onnx.data</c>, <c>genai_config.json</c>). When <c>null</c> or whitespace,
    /// no <see cref="OgaModelSession"/> is registered and the registry is bound with a
    /// null session — Activate calls will return a Failure asking for a session.
    /// The model is loaded lazily on first <see cref="OgaModelSession.RegisterAdapter"/>
    /// call, so registering with a path that does not yet exist on disk does not throw
    /// at DI build time.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Phase A.4: this overload is the wiring entry point that turns the Phase-A scaffold
    /// from "load from disk + return Failure on Activate" into a fully wired registry.
    /// </remarks>
    public static IServiceCollection AddOgaAdapterRegistry(
        this IServiceCollection services,
        string adapterRootPath,
        string? modelPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterRootPath);

        if (!string.IsNullOrWhiteSpace(modelPath))
        {
            services.TryAddSingleton<OgaModelSession>(sp =>
            {
                var sessionLogger = sp.GetService<ILogger<OgaModelSession>>();
                return new OgaModelSession(modelPath, sessionLogger);
            });
        }

        services.TryAddSingleton<IAdapterRegistry>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<OgaAdapterRegistry>>();
            OgaModelSession? session = string.IsNullOrWhiteSpace(modelPath)
                ? null
                : sp.GetRequiredService<OgaModelSession>();
            return new OgaAdapterRegistry(adapterRootPath, logger, session);
        });

        return services;
    }
}
