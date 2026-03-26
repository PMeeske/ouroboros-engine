// <copyright file="TensorBackendExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ouroboros.Tensor.Backends;
using Ouroboros.Tensor.Configuration;
using Ouroboros.Tensor.Models;

namespace Ouroboros.Tensor.Extensions;

/// <summary>
/// Extension methods for registering tensor backends with dependency injection.
/// </summary>
public static class TensorBackendExtensions
{
    /// <summary>
    /// Registers <see cref="RemoteTensorBackend"/> with the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration callback for tensor service options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Registers:
    /// <list type="bullet">
    /// <item><description>TensorServiceOptions as singleton (configurable)</description></item>
    /// <item><description>TensorServiceClient with HttpClientFactory</description></item>
    /// <item><description>ITensorBackend → RemoteTensorBackend as singleton</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The remote backend calls a Docker PyTorch service for GPU-accelerated operations.
    /// Falls back to CPU if the remote service is unavailable.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddRemoteTensorBackend(
        this IServiceCollection services,
        Action<TensorServiceOptions>? configure = null)
    {
        var options = new TensorServiceOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddHttpClient<TensorServiceClient>(client =>
        {
            client.BaseAddress = options.BaseUrl;
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        services.AddSingleton<ITensorBackend, RemoteTensorBackend>();

        return services;
    }

    /// <summary>
    /// Registers <see cref="RemoteTensorBackend"/> as primary with <see cref="CpuTensorBackend"/> as fallback.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration callback for tensor service options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This pattern provides resilience:
    /// <list type="bullet">
    /// <item><description>Primary: RemoteTensorBackend (GPU via Docker PyTorch)</description></item>
    /// <item><description>Fallback: CpuTensorBackend (CPU SIMD via TensorPrimitives)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// If the remote service fails, fallback to CpuTensorBackend is handled by the consumer.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddTensorBackendWithFallback(
        this IServiceCollection services,
        Action<TensorServiceOptions>? configure = null)
    {
        var options = new TensorServiceOptions();
        configure?.Invoke(options);

        // Register options
        services.AddSingleton(options);

        // Register HttpClient for TensorServiceClient
        services.AddHttpClient<TensorServiceClient>(client =>
        {
            client.BaseAddress = options.BaseUrl;
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        // Register RemoteTensorBackend as primary ITensorBackend
        services.AddSingleton<ITensorBackend, RemoteTensorBackend>();

        // Register CpuTensorBackend as fallback (named)
        services.TryAddSingleton(CpuTensorBackend.Instance);

        return services;
    }

    /// <summary>
    /// Registers <see cref="CpuTensorBackend"/> as the sole tensor backend.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Use this when GPU acceleration is not needed or unavailable.
    /// </remarks>
    public static IServiceCollection AddCpuTensorBackend(this IServiceCollection services)
    {
        services.AddSingleton<ITensorBackend>(CpuTensorBackend.Instance);
        return services;
    }
}