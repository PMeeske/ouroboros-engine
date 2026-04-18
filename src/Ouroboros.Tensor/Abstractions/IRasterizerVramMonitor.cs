// <copyright file="IRasterizerVramMonitor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Abstractions;

/// <summary>
/// Tensor-tier seam for rasterizer VRAM register/release accounting. The
/// concrete implementation lives in the App tier (<c>VramBudgetMonitor</c>
/// under the <c>Rasterizer</c> bucket); the Tensor project only depends on
/// this interface so <see cref="IGaussianRasterizer"/> implementations
/// (notably <c>DirectComputeGaussianRasterizer</c>) can register their
/// 128 MiB budget at init without an upward tier reference.
/// </summary>
/// <remarks>
/// Phase 188.1.1 plan 03 — introduced to close the cross-tier dependency
/// (<c>Ouroboros.Application.Avatar.VramBudgetMonitor</c> is not visible
/// from <c>Ouroboros.Tensor</c>). Null-object default: a rasterizer
/// constructed without a monitor behaves identically to today, skipping
/// the register/release pair.
/// </remarks>
public interface IRasterizerVramMonitor
{
    /// <summary>Records that the rasterizer is holding <paramref name="bytes"/> of VRAM.</summary>
    void RegisterRasterizerAllocation(long bytes);

    /// <summary>Symmetric release — deducts <paramref name="bytes"/> from the tracked total.</summary>
    void ReleaseRasterizerAllocation(long bytes);
}
