// <copyright file="ITensorBackendSelector.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Abstractions;

/// <summary>
/// Selects an <see cref="ITensorBackend"/> at runtime based on the requested device and availability.
/// Implementations may probe the hardware (e.g. CUDA availability) or consult configuration
/// to return the appropriate backend (R14).
/// </summary>
public interface ITensorBackendSelector
{
    /// <summary>
    /// Gets a value indicating whether gets whether a GPU device is available on the current system.
    /// </summary>
    bool IsGpuAvailable { get; }

    /// <summary>
    /// Returns the best available backend for the requested <paramref name="preferred"/> device.
    /// Falls back to CPU when the preferred device is unavailable.
    /// </summary>
    /// <param name="preferred">
    /// The desired device. Defaults to <see cref="DeviceType.Cpu"/>.
    /// </param>
    /// <returns>A backend instance appropriate for the device. Never <see langword="null"/>.</returns>
    ITensorBackend SelectBackend(DeviceType preferred = DeviceType.Cpu);
}
