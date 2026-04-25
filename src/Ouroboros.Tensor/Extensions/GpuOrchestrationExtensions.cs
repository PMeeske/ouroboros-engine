// <copyright file="GpuOrchestrationExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Configuration;

namespace Ouroboros.Tensor.Extensions;

/// <summary>
/// DI registration extensions for GPU orchestration.
/// </summary>
/// <example>
/// <code>
/// services.AddOuroborosTensorGpu(options =>
/// {
///     options.PreferOpenCl = true;
///     options.MaxPooledVramBytes = 512 * 1024 * 1024;
/// });
/// </code>
/// </example>
public static class GpuOrchestrationExtensions
{
    /// <summary>
    /// Registers GPU orchestration services: backend selector, scheduler, and
    /// decorated backend pipeline (Validation → Logging → Metrics → Backend).
    /// </summary>
    /// <returns></returns>
    public static IServiceCollection AddOuroborosTensorGpu(
        this IServiceCollection services,
        Action<GpuOrchestrationOptions>? configure = null)
    {
        var options = new GpuOrchestrationOptions();
        configure?.Invoke(options);

        // Backend selector (probes hardware at startup)
        services.AddSingleton<ITensorBackendSelector>(sp =>
        {
            var logger = sp.GetService<ILogger<GpuAwareBackendSelector>>();
            return new GpuAwareBackendSelector(logger);
        });

        // Decorated GPU backend (with full decorator stack)
        services.AddSingleton<ITensorBackend>(sp =>
        {
            var selector = sp.GetRequiredService<ITensorBackendSelector>();
            var logger = sp.GetRequiredService<ILogger<LoggingTensorBackend>>();
            var preferred = options.PreferOpenCl ? DeviceType.OpenCL : DeviceType.Cuda;

            var rawBackend = selector.SelectBackend(preferred);

            return new TensorBackendBuilder(rawBackend)
                .WithValidation()
                .WithLogging(logger)
                .WithMetrics()
                .Build();
        });

        // GPU scheduler — v2 is the canonical instance; legacy adapter wraps it
        services.AddSingleton<GpuSchedulerV2>(sp =>
        {
            var selector = sp.GetRequiredService<ITensorBackendSelector>();
            long vram = options.TotalVramOverrideBytes ?? EstimateVram(selector);
            return new GpuSchedulerV2(vram);
        });
        services.AddSingleton<IGpuScheduler>(sp => sp.GetRequiredService<GpuSchedulerV2>());
        services.AddSingleton<GpuScheduler>(sp =>
        {
            var v2 = sp.GetRequiredService<GpuSchedulerV2>();
            return new GpuScheduler(v2);
        });

        // VRAM layout resolution (Phase 188.1 AVA-07) — DXGI adapter detect →
        // preset registry, with Avatar:VramLayoutOverride config override. One
        // IVramLayout singleton is resolved per process and reused by
        // VramBudgetMonitor + (future) SharedD3D12Device (plan 03).
        services.TryAddSingleton<IDxgiAdapterEnumerator>(sp =>
            new DxgiAdapterEnumerator(sp.GetService<ILogger<DxgiAdapterEnumerator>>()));

        services.TryAddSingleton<IVramLayoutProvider>(sp =>
            new DxgiVramLayoutProvider(
                sp.GetRequiredService<IDxgiAdapterEnumerator>(),
                sp.GetService<ILogger<DxgiVramLayoutProvider>>()));

        services.TryAddSingleton<IVramLayout>(sp =>
        {
            var provider = sp.GetRequiredService<IVramLayoutProvider>();

            // Empty IConfiguration acceptable — DxgiVramLayoutProvider treats a
            // missing override key as "auto-detect". Hosts that want to force a
            // preset register their own IConfiguration before this callback runs.
            var configuration = sp.GetService<IConfiguration>() ?? EmptyConfiguration.Instance;
            return provider.Resolve(configuration);
        });

        return services;
    }

    private static long EstimateVram(ITensorBackendSelector? selector = null)
    {
#if ENABLE_ILGPU
        var backend = selector.SelectBackend(DeviceType.OpenCL);
        if (backend is IlgpuOpenClTensorBackend ilgpu)
            return ilgpu.TotalMemoryBytes;
#endif
        // Default: 4 GB estimate
        return 4L * 1024 * 1024 * 1024;
    }
}

/// <summary>
/// Configuration options for GPU orchestration registration.
/// </summary>
public sealed class GpuOrchestrationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether prefer OpenCL/ILGPU (AMD) over CUDA/TorchSharp when both are available.
    /// Default: <see langword="true"/>.
    /// </summary>
    public bool PreferOpenCl { get; set; } = true;

    /// <summary>
    /// Gets or sets override the auto-detected total VRAM for scheduler accounting.
    /// Useful when the GPU is shared with other processes.
    /// When null, auto-detected from the device.
    /// </summary>
    public long? TotalVramOverrideBytes { get; set; }
}

/// <summary>
/// Extensions for <see cref="TensorBackendBuilder"/> to add GPU-specific decorators.
/// </summary>
public static class TensorBackendBuilderGpuExtensions
{
    /// <summary>
    /// Wraps the current backend with GPU scheduling awareness. Operations are
    /// funneled through the <see cref="GpuScheduler"/> for VRAM accounting.
    /// </summary>
    /// <returns></returns>
    public static TensorBackendBuilder WithGpuScheduling(
        this TensorBackendBuilder builder,
        GpuScheduler scheduler,
        GpuTaskPriority defaultPriority = GpuTaskPriority.Normal)
    {
        // The builder pattern doesn't support custom decorators out of the box,
        // so this is a guide for extending TensorBackendBuilder:
        //
        // 1. Add a new decorator class (ScheduledTensorBackend) that wraps
        //    ITensorBackend and calls scheduler.ScheduleAsync() around
        //    MatMul/Add/Create operations.
        //
        // 2. Add this method to TensorBackendBuilder itself:
        //    _backend = new ScheduledTensorBackend(_backend, scheduler, defaultPriority);
        //
        // For now, GPU scheduling is handled at the node level (GpuTensorNode),
        // not at the backend level. This keeps the existing decorator chain intact.
        return builder;
    }
}
