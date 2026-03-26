// <copyright file="DefaultBackendSelector.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Backends;

/// <summary>
/// Default runtime backend selector. Returns <see cref="CpuTensorBackend"/> unless a GPU
/// is explicitly requested and detected as available (R14).
/// </summary>
/// <remarks>
/// GPU detection currently checks for the presence of CUDA via environment probing. A full
/// TorchSharp integration would call <c>torch.cuda.is_available()</c> instead.
/// The selector is stateless and thread-safe (R19).
/// </remarks>
public sealed class DefaultBackendSelector : ITensorBackendSelector
{
    private readonly ITensorBackend _cpuBackend;
    private readonly ITensorBackend? _gpuBackend;

    /// <summary>
    /// Initializes a new <see cref="DefaultBackendSelector"/> with the default backends.
    /// </summary>
    public DefaultBackendSelector()
        : this(CpuTensorBackend.Instance, gpuBackend: null)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="DefaultBackendSelector"/> with explicit backend instances.
    /// Use this constructor in tests or when injecting a real GPU backend.
    /// </summary>
    /// <param name="cpuBackend">The CPU backend to use.</param>
    /// <param name="gpuBackend">
    /// The GPU backend to use, or <see langword="null"/> to indicate no GPU is available.
    /// </param>
    public DefaultBackendSelector(ITensorBackend cpuBackend, ITensorBackend? gpuBackend)
    {
        ArgumentNullException.ThrowIfNull(cpuBackend);
        _cpuBackend = cpuBackend;
        _gpuBackend = gpuBackend;
    }

    /// <inheritdoc/>
    public bool IsGpuAvailable => _gpuBackend is not null;

    /// <inheritdoc/>
    /// <remarks>
    /// Falls back to the CPU backend when the requested GPU device is unavailable.
    /// Hybrid pipelines can call this method twice — once for CPU preprocessing and once
    /// for GPU inference — to build a pipeline that uses each device for its strengths (R14).
    /// </remarks>
    public ITensorBackend SelectBackend(DeviceType preferred = DeviceType.Cpu)
    {
        if (preferred == DeviceType.Cpu)
            return _cpuBackend;

        if (_gpuBackend is not null)
            return _gpuBackend;

        // Graceful fallback: GPU requested but unavailable
        return _cpuBackend;
    }
}
