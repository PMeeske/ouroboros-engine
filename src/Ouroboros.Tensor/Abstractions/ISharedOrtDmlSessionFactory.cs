// <copyright file="ISharedOrtDmlSessionFactory.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.ML.OnnxRuntime;

namespace Ouroboros.Tensor.Abstractions;

/// <summary>
/// Produces <see cref="SessionOptions"/> bound to the process-wide shared
/// D3D12 device by LUID. Every ORT DirectML session construction site in
/// the engine + app layers must route through this seam so ORT's internal
/// <c>D3D12CreateDevice(deviceId)</c> call lands on the same adapter that
/// <c>SharedD3D12Device</c> attached to — at which point D3D12's
/// per-adapter singleton guarantee yields one <c>ID3D12Device</c> across
/// all sessions plus the compute-shader rasterizer.
/// </summary>
/// <remarks>
/// <para>
/// Phase 196.3 infrastructure (COORD-DEV-01). The interface lives in
/// <c>Ouroboros.Tensor.Abstractions</c> so both Engine and App layer
/// consumers depend only on the contract — the implementation
/// (<c>SharedOrtDmlSessionFactory</c>) + its DXGI ordinal resolver stay in
/// <c>Ouroboros.Tensor</c>.
/// </para>
/// <para>
/// <b>Caller contract:</b> consumers MUST NOT call
/// <see cref="SessionOptions.AppendExecutionProvider_DML(int)"/> on the
/// returned object — the factory already did. The mandatory DML quirks
/// (<c>session.disable_mem_pattern=1</c>, <see cref="ExecutionMode.ORT_SEQUENTIAL"/>,
/// <see cref="GraphOptimizationLevel.ORT_ENABLE_ALL"/>) are also pre-applied;
/// direct <c>new SessionOptions()</c> in a DML context becomes a code-review
/// smell after this lands.
/// </para>
/// <para>
/// <b>Disposal:</b> the returned <see cref="SessionOptions"/> is owned by
/// the caller — dispose after the <c>InferenceSession</c> is constructed.
/// </para>
/// <para>
/// <b>Failure mode:</b> when the shared device is unavailable (non-Windows
/// host, sentinel LUID, driver missing), <see cref="CreateSessionOptions"/>
/// throws <see cref="InvalidOperationException"/>. Callers that support a
/// CPU-only lane must catch and fall back; callers that require DML must
/// let it propagate.
/// </para>
/// </remarks>
public interface ISharedOrtDmlSessionFactory
{
    /// <summary>
    /// Builds a <see cref="SessionOptions"/> pre-configured with the DML
    /// execution provider bound to the shared-device adapter LUID, plus
    /// the mandatory DML quirks.
    /// </summary>
    /// <returns>
    /// A fresh disposable <see cref="SessionOptions"/>. Caller owns
    /// disposal. Do not call <c>AppendExecutionProvider_DML</c> again.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the shared D3D12 device is unavailable (non-Windows,
    /// sentinel LUID, native failure) or when no DXGI adapter matches
    /// the shared device's resolved LUID.
    /// </exception>
    SessionOptions CreateSessionOptions();
}
