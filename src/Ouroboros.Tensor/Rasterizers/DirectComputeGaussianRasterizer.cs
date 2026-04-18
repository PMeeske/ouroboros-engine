// <copyright file="DirectComputeGaussianRasterizer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Orchestration;

namespace Ouroboros.Tensor.Rasterizers;

/// <summary>
/// HLSL compute-shader rasterizer. Implements <see cref="IGaussianRasterizer"/>
/// and is the production DI registration for the C# 3DGS live-render path.
/// Every <see cref="RasterizeAsync"/> invocation routes through exactly one
/// <see cref="GpuScheduler.ScheduleAsync{T}(GpuTaskPriority, GpuResourceRequirements, Func{Task{T}}, CancellationToken)"/>
/// call with <see cref="RasterizerRequirements.Realtime"/>, preserving the
/// VRAM budget + priority pre-emption guarantees established by Phase 188.1.
/// </summary>
/// <remarks>
/// <para>
/// Init gate (Phase 188.1.1 plan 03): on construction, the rasterizer asks
/// the injected <see cref="SharedD3D12Device"/> whether D3D12 is available
/// and asks the injected <see cref="HlslShaderLoader"/> to resolve the four
/// DXIL resources shipped by Plan 02. If either check fails, the class
/// latches a permanent CPU-fallback state — all subsequent
/// <see cref="RasterizeAsync"/> calls delegate to an inner
/// <see cref="CpuGaussianRasterizer"/>. One WARN log fires exactly once at
/// init; subsequent invocations stay quiet.
/// </para>
/// <para>
/// Partial-class split (kept under 500 LOC each):
/// <list type="bullet">
///   <item><b>This file</b>: public surface — constructor, init gate, dispose,
///     <see cref="RasterizeAsync"/> scheduler routing, telemetry.</item>
///   <item><c>.Dispatch.cs</c>: D3D12 buffer allocation, PSO creation, the
///     per-frame upload → project → tile-assign → tile-sort → tile-raster →
///     readback pipeline. Plan 03 shipped the CPU-delegating stub; the real
///     command-list recording follows in a focused follow-up once the
///     root-signature + descriptor-table contract stabilises.</item>
/// </list>
/// </para>
/// </remarks>
public sealed partial class DirectComputeGaussianRasterizer : IGaussianRasterizer, IDisposable
{
    private static readonly IReadOnlyList<string> RequiredShaders =
    [
        "gaussian_project",
        "gaussian_tile_assign",
        "gaussian_tile_sort",
        "gaussian_tile_raster",
    ];

    private readonly CpuGaussianRasterizer _cpuFallback;
    private readonly GpuScheduler? _scheduler;
    private readonly SharedD3D12Device? _sharedDevice;
    private readonly HlslShaderLoader? _shaderLoader;
    private readonly IRasterizerVramMonitor? _vramBudget;
    private readonly ILogger _logger;

    private readonly object _initLock = new();
    private bool _initAttempted;
    private bool _cpuLatched;
    private bool _wasGpuDispatched;
    private IReadOnlyDictionary<string, byte[]>? _loadedShaders;
    private bool _vramRegistered;
    private int _disposed;

    /// <summary>
    /// Simplified constructor for scenarios where D3D12 infrastructure is
    /// unavailable (tests, non-Windows hosts, early-boot paths). Always
    /// latches CPU fallback.
    /// </summary>
    public DirectComputeGaussianRasterizer(
        GpuScheduler? scheduler = null,
        ILogger<DirectComputeGaussianRasterizer>? logger = null)
        : this(scheduler, sharedDevice: null, shaderLoader: null, vramBudget: null, logger: logger)
    {
    }

    /// <summary>
    /// Production DI constructor. <paramref name="sharedDevice"/> is wired
    /// by <c>GaussianRasterizerExtensions.AddDirectComputeGaussianRasterizer</c>;
    /// a null instance or an unavailable device forces CPU latch.
    /// </summary>
    public DirectComputeGaussianRasterizer(
        GpuScheduler? scheduler,
        SharedD3D12Device? sharedDevice,
        HlslShaderLoader? shaderLoader,
        IRasterizerVramMonitor? vramBudget,
        ILogger<DirectComputeGaussianRasterizer>? logger)
    {
        _logger = logger ?? NullLogger<DirectComputeGaussianRasterizer>.Instance;
        _cpuFallback = new CpuGaussianRasterizer(scheduler);
        _scheduler = scheduler;
        _sharedDevice = sharedDevice;
        _shaderLoader = shaderLoader;
        _vramBudget = vramBudget;
    }

    /// <summary>
    /// <see langword="true"/> once any call has successfully dispatched on
    /// the GPU at least once. Plan 188.1.1-04's pixel-diff test uses this
    /// flag to guard against silent CPU fallbacks passing the MAD gate
    /// trivially (since both paths run the same algorithm until the real
    /// HLSL dispatch body lands).
    /// </summary>
    public bool WasGpuDispatched => _wasGpuDispatched;

    /// <summary>
    /// <see langword="true"/> when init has run and latched permanent CPU
    /// fallback. Public for diagnostics + test surfaces.
    /// </summary>
    public bool IsCpuLatched
    {
        get
        {
            EnsureInitialized();
            return _cpuLatched;
        }
    }

    /// <inheritdoc />
    public Task<FrameBuffer> RasterizeAsync(
        GaussianSet gaussians,
        CameraParams camera,
        GpuResourceRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gaussians);
        ArgumentNullException.ThrowIfNull(camera);
        EnsureInitialized();

        if (_cpuLatched)
        {
            // Latched — skip scheduler indirection; the CPU rasterizer
            // already routes through its own GpuScheduler wiring.
            return _cpuFallback.RasterizeAsync(gaussians, camera, requirements, cancellationToken);
        }

        if (_scheduler is null)
        {
            return DispatchAsync(gaussians, camera, cancellationToken);
        }

        return _scheduler.ScheduleAsync(
            GpuTaskPriority.Realtime,
            requirements,
            () => DispatchAsync(gaussians, camera, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_vramRegistered && _vramBudget is not null)
        {
            _vramBudget.ReleaseRasterizerAllocation(RasterizerRequirements.Realtime.EstimatedVramBytes);
            _vramRegistered = false;
        }

        ReleaseGpuResources();
    }

    private void EnsureInitialized()
    {
        if (_initAttempted)
        {
            return;
        }

        lock (_initLock)
        {
            if (_initAttempted)
            {
                return;
            }

            try
            {
                TryInitializeGpu();
            }
#pragma warning disable CA1031 // init failure must NEVER throw — always latches CPU fallback.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogWarning(
                    ex,
                    "[DirectComputeGaussianRasterizer] init threw — latching CPU fallback permanently");
                LatchCpu(reason: $"init threw: {ex.GetType().Name}");
            }
            finally
            {
                _initAttempted = true;
            }
        }
    }

    private void TryInitializeGpu()
    {
        if (_sharedDevice is null)
        {
            LatchCpu(reason: "SharedD3D12Device not injected (test / simplified-ctor path)");
            return;
        }

        if (!_sharedDevice.IsAvailable)
        {
            LatchCpu(reason: $"SharedD3D12Device unavailable (LUID=0x{_sharedDevice.ResolvedAdapterLuid:X16})");
            return;
        }

        if (_shaderLoader is null)
        {
            LatchCpu(reason: "HlslShaderLoader not injected");
            return;
        }

        if (!_shaderLoader.TryLoadAll(RequiredShaders, out IReadOnlyDictionary<string, byte[]> loaded))
        {
            LatchCpu(reason: "one or more required DXIL resources missing (DXC not run / non-Windows build)");
            return;
        }
        _loadedShaders = loaded;

        if (_vramBudget is not null)
        {
            _vramBudget.RegisterRasterizerAllocation(RasterizerRequirements.Realtime.EstimatedVramBytes);
            _vramRegistered = true;
        }

        _logger.LogInformation(
            "[DirectComputeGaussianRasterizer] GPU path armed — LUID=0x{Luid:X16}, {Count} DXIL resources loaded, VRAM reserved={Mb}MiB",
            _sharedDevice.ResolvedAdapterLuid,
            RequiredShaders.Count,
            RasterizerRequirements.Realtime.EstimatedVramBytes / (1024L * 1024L));
    }

    private void LatchCpu(string reason)
    {
        if (_cpuLatched) return;
        _cpuLatched = true;
        _logger.LogWarning(
            "[DirectComputeGaussianRasterizer] CPU fallback latched: {Reason}",
            reason);
    }

    private async Task<FrameBuffer> DispatchAsync(
        GaussianSet gaussians,
        CameraParams camera,
        CancellationToken cancellationToken)
    {
        // Delegate to the partial-class dispatch body. When that path completes
        // a real GPU dispatch, it sets _wasGpuDispatched = true; until then
        // it runs the CPU baseline for byte-identical output with
        // CpuGaussianRasterizer so the plan-04 pixel-diff gate passes
        // trivially while the command-list body is authored.
        return await DispatchInternalAsync(gaussians, camera, cancellationToken).ConfigureAwait(false);
    }
}
