// <copyright file="GpuAwareBackendSelector.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Backends;

/// <summary>
/// Extended backend selector that probes for AMD GPUs via ILGPU/OpenCL in addition to
/// CUDA via TorchSharp. Falls back gracefully: ROCm/OpenCL → CUDA → CPU (R14).
/// </summary>
/// <remarks>
/// <para>
/// Replaces <see cref="DefaultBackendSelector"/> when GPU orchestration is enabled.
/// Register via DI:
/// <code>
/// services.AddSingleton&lt;ITensorBackendSelector, GpuAwareBackendSelector&gt;();
/// </code>
/// </para>
/// <para>
/// GPU detection is performed once at construction time. The selected backends are
/// cached and reused for all subsequent calls. Thread-safe (R19).
/// </para>
/// </remarks>
public sealed class GpuAwareBackendSelector : ITensorBackendSelector
{
    private readonly ITensorBackend _cpuBackend;
    private readonly ITensorBackend? _openClBackend;
    private readonly ITensorBackend? _cudaBackend;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GpuAwareBackendSelector"/> class.
    /// Initializes the selector, probing for available GPU backends.
    /// </summary>
    /// <param name="logger">Optional logger for reporting detected devices.</param>
    public GpuAwareBackendSelector(ILogger<GpuAwareBackendSelector>? logger = null)
    {
        _logger = logger;
        _cpuBackend = CpuTensorBackend.Instance;

        // Probe ILGPU/OpenCL (AMD ROCm)
        _openClBackend = TryCreateOpenClBackend();

        // Probe TorchSharp/CUDA (NVIDIA)
        _cudaBackend = TryCreateCudaBackend();

        if (_openClBackend is not null)
        {
            _logger?.LogInformation("GPU detected: OpenCL/ILGPU backend available");
        }

        if (_cudaBackend is not null)
        {
            _logger?.LogInformation("GPU detected: CUDA/TorchSharp backend available");
        }

        if (!IsGpuAvailable)
        {
            _logger?.LogInformation(
                "No generic tensor GPU backend available. OpenCL/CUDA tensor operations will use CPU; DirectML-backed ONNX inference is configured separately.");
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GpuAwareBackendSelector"/> class.
    /// Initializes with explicit backend instances (for testing).
    /// </summary>
    public GpuAwareBackendSelector(
        ITensorBackend cpuBackend,
        ITensorBackend? openClBackend,
        ITensorBackend? cudaBackend)
    {
        ArgumentNullException.ThrowIfNull(cpuBackend);
        _cpuBackend = cpuBackend;
        _openClBackend = openClBackend;
        _cudaBackend = cudaBackend;
    }

    /// <inheritdoc/>
    public bool IsGpuAvailable => _openClBackend is not null || _cudaBackend is not null;

    /// <summary>Gets a value indicating whether gets whether an OpenCL/ILGPU (AMD) backend is available.</summary>
    public bool IsOpenClAvailable => _openClBackend is not null;

    /// <summary>Gets a value indicating whether gets whether a CUDA/TorchSharp (NVIDIA) backend is available.</summary>
    public bool IsCudaAvailable => _cudaBackend is not null;

    /// <inheritdoc/>
    /// <remarks>
    /// Selection priority:
    /// <list type="number">
    ///   <item><see cref="DeviceType.Cpu"/> → always returns CPU backend</item>
    ///   <item><see cref="DeviceType.OpenCL"/> or <see cref="DeviceType.Rocm"/> → ILGPU backend</item>
    ///   <item><see cref="DeviceType.Cuda"/> → TorchSharp backend</item>
    ///   <item>Any other GPU type → try OpenCL first, then CUDA, then CPU</item>
    /// </list>
    /// </remarks>
    public ITensorBackend SelectBackend(DeviceType preferred = DeviceType.Cpu)
    {
        return preferred switch
        {
            DeviceType.Cpu => _cpuBackend,

            DeviceType.OpenCL or DeviceType.Rocm =>
                _openClBackend ?? _cpuBackend,

            DeviceType.Cuda =>
                _cudaBackend ?? _openClBackend ?? _cpuBackend,

            // Any GPU request: prefer OpenCL (AMD) → CUDA → CPU
            _ => _openClBackend ?? _cudaBackend ?? _cpuBackend,
        };
    }

    /// <summary>
    /// Returns the best available GPU backend regardless of vendor preference.
    /// Useful when you just want "any GPU".
    /// </summary>
    /// <returns></returns>
    public ITensorBackend SelectBestGpu()
        => _openClBackend ?? _cudaBackend ?? _cpuBackend;

    private ITensorBackend? TryCreateOpenClBackend()
    {
#if ENABLE_ILGPU
        try
        {
            return new IlgpuOpenClTensorBackend();
        }
        catch (NotSupportedException ex)
        {
            _logger?.LogDebug(ex, "ILGPU/OpenCL backend not available");
            return null;
        }
#pragma warning disable CA1031 // Any unexpected ILGPU init failure should degrade to CPU
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger?.LogWarning(ex, "Failed to initialise ILGPU/OpenCL backend");
            return null;
        }
#else
        return null;
#endif
    }

    private ITensorBackend? TryCreateCudaBackend()
    {
#if ENABLE_GPU
        try
        {
            return new TorchSharpGpuTensorBackend();
        }
        catch (NotSupportedException ex)
        {
            _logger?.LogDebug(ex, "TorchSharp/CUDA backend not available");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to initialise TorchSharp/CUDA backend");
            return null;
        }
#else
        return null;
#endif
    }
}
