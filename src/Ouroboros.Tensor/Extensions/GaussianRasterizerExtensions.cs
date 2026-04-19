// <copyright file="GaussianRasterizerExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Backends;
using Ouroboros.Tensor.Orchestration;
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

    /// <summary>
    /// Replaces the default <see cref="IGaussianRasterizer"/> with the HLSL
    /// surface (<see cref="DirectComputeGaussianRasterizer"/>). Phase 188.1
    /// plan 03 ships the adapter surface; the HLSL compute-shader dispatch
    /// body is a follow-up (188.1.1) and the current implementation
    /// delegates to the CPU baseline internally, so swapping in via this
    /// extension is safe today and will pick up real GPU dispatch without
    /// any caller-side changes.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns><paramref name="services"/>, for fluent chaining.</returns>
    public static IServiceCollection AddDirectComputeGaussianRasterizer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register SharedD3D12Device as a singleton that resolves its adapter
        // from the registered IVramLayout (Phase 188.1-01). TryCreate never
        // throws — when the layout's AdapterLuid is 0UL (in-code preset) or
        // device creation fails for any reason, the instance is returned with
        // IsAvailable=false and DirectComputeGaussianRasterizer falls back to
        // the CPU baseline. Registered BEFORE the rasterizer so its ctor can
        // inject it via the standard DI resolution path.
        services.TryAddSingleton<SharedD3D12Device>(sp =>
        {
            IVramLayout layout = sp.GetRequiredService<IVramLayout>();
            ILogger<SharedD3D12Device>? logger = sp.GetService<ILogger<SharedD3D12Device>>();
            return SharedD3D12Device.TryCreate(layout, logger);
        });

        // Phase 188.1.1-03 — HlslShaderLoader provides embedded-resource DXIL
        // access for the four compute shaders authored in plan 02. Loader is
        // stateless + lazy; assembly lookup only runs when TryLoadAll is called
        // from the rasterizer's init gate.
        services.TryAddSingleton<HlslShaderLoader>(sp =>
            new HlslShaderLoader(sp.GetService<ILogger<HlslShaderLoader>>()));

        // Phase 196.3-01 — ISharedOrtDmlSessionFactory is the single
        // construction seam for ORT DirectML sessions across engine + app.
        // Resolver is singleton so its per-LUID cache survives the process;
        // factory is singleton so its Lazy<int> ordinal + one-shot binding
        // log fire exactly once. TryAddSingleton lets a host override the
        // resolver (e.g. test seam feeding AdapterInfo directly) without
        // duplicating registrations.
        services.TryAddSingleton<DxgiAdapterLuidResolver>(sp =>
        {
            IDxgiAdapterEnumerator? enumerator = sp.GetService<IDxgiAdapterEnumerator>();
            return enumerator is null
                ? new DxgiAdapterLuidResolver()
                : new DxgiAdapterLuidResolver(enumerator);
        });
        services.TryAddSingleton<ISharedOrtDmlSessionFactory, SharedOrtDmlSessionFactory>();

        services.AddSingleton<IGaussianRasterizer>(sp =>
        {
            GpuScheduler? scheduler = sp.GetService<GpuScheduler>();
            SharedD3D12Device? shared = sp.GetService<SharedD3D12Device>();
            HlslShaderLoader? loader = sp.GetService<HlslShaderLoader>();
            IRasterizerVramMonitor? vram = sp.GetService<IRasterizerVramMonitor>();
            ILogger<DirectComputeGaussianRasterizer>? logger =
                sp.GetService<ILogger<DirectComputeGaussianRasterizer>>();
            return new DirectComputeGaussianRasterizer(scheduler, shared, loader, vram, logger);
        });
        return services;
    }
}
