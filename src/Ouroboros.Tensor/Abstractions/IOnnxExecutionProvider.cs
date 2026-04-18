// <copyright file="IOnnxExecutionProvider.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.ML.OnnxRuntime;

namespace Ouroboros.Tensor.Abstractions;

/// <summary>
/// Abstracts ONNX Runtime execution provider configuration so inference code
/// is decoupled from a specific EP (DirectML, Windows ML, CPU). Promoted from
/// <c>Ouroboros.Application.Avatar</c> in Phase 188.1 plan 03 so the new
/// <c>DirectComputeGaussianRasterizer</c> and any other Tensor-project adapter
/// can consume the same contract without reaching up into the App layer.
/// </summary>
public interface IOnnxExecutionProvider
{
    /// <summary>Configures the given <see cref="SessionOptions"/> with this EP.</summary>
    /// <returns><c>true</c> if GPU acceleration was successfully enabled.</returns>
    bool Configure(SessionOptions options);

    /// <summary>
    /// Device allocator name for <see cref="OrtMemoryInfo"/> when creating
    /// IOBinding-based zero-copy GPU tensors (e.g. <c>"DML"</c> for DirectML).
    /// <c>null</c> when the EP does not support device allocation.
    /// </summary>
    string? DeviceAllocatorName { get; }

    /// <summary>Human-readable name for diagnostics/logging.</summary>
    string Name { get; }
}
