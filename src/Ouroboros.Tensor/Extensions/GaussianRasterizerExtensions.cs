// <copyright file="GaussianRasterizerExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Rasterizers;

namespace Ouroboros.Tensor.Extensions;

/// <summary>
/// DI extensions for wiring <see cref="IGaussianRasterizer"/> implementations.
/// The CPU rasterizer is the default — plan 03's HLSL
/// <c>DirectComputeGaussianRasterizer</c> replaces it via a second call to
/// <see cref="AddDirectComputeGaussianRasterizer"/> (which that plan ships).
/// </summary>
public static class GaussianRasterizerExtensions
{
    /// <summary>
    /// Registers <see cref="CpuGaussianRasterizer"/> as the default
    /// <see cref="IGaussianRasterizer"/>. Safe to call multiple times; uses
    /// <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton{T}(IServiceCollection)"/>
    /// so HLSL replacement wins when it registers later.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns><paramref name="services"/>, for fluent chaining.</returns>
    public static IServiceCollection AddGaussianRasterizer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IGaussianRasterizer, CpuGaussianRasterizer>();
        return services;
    }
}
