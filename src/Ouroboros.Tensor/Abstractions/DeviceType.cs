// <copyright file="DeviceType.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Abstractions;

/// <summary>
/// Identifies the compute device on which a tensor resides.
/// </summary>
public enum DeviceType
{
    /// <summary>CPU (system memory).</summary>
    Cpu,

    /// <summary>NVIDIA CUDA GPU.</summary>
    Cuda,

    /// <summary>Apple Metal Performance Shaders GPU.</summary>
    Mps,

    /// <summary>Microsoft DirectML GPU (AMD/Intel/NVIDIA via WDDM).</summary>
    DirectML,
}
