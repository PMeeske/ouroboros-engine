// <copyright file="TensorToBackendAdapter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Enforces the execution boundary between the data layer (<see cref="ITensor{T}"/> / .NET buffers)
/// and the backend execution layer (R13). Ensures tensors are on the correct device before
/// operations are dispatched to the backend, preventing uncontrolled device round-trips.
/// </summary>
public static class TensorToBackendAdapter
{
    /// <summary>
    /// Ensures that <paramref name="tensor"/> is resident on <paramref name="backend"/>s device
    /// before use, transferring it if necessary. Records whether a transfer occurred so that callers
    /// can track the associated cost (R03, R13).
    /// </summary>
    /// <param name="tensor">The source tensor.</param>
    /// <param name="backend">The target backend.</param>
    /// <returns>
    /// <c>(tensor, transferred: false)</c> if the tensor is already on the correct device;
    /// <c>(transferred tensor, transferred: true)</c> after an explicit device move.
    /// The returned tensor must be disposed by the caller if <c>transferred</c> is
    /// <see langword="true"/> (the original tensor remains the caller's responsibility otherwise).
    /// </returns>
    public static (ITensor<float> Tensor, bool Transferred) EnsureDevice(
        ITensor<float> tensor,
        ITensorBackend backend)
    {
        ArgumentNullException.ThrowIfNull(tensor);
        ArgumentNullException.ThrowIfNull(backend);

        if (tensor.Device == backend.Device)
            return (tensor, Transferred: false);

        // Explicit cross-device transfer
        var transferred = backend.Device == DeviceType.Cpu
            ? tensor.ToCpu()
            : tensor.ToGpu();

        return (transferred, Transferred: true);
    }

    /// <summary>
    /// Validates that two tensors are compatible for a binary operation and both reside on
    /// the backend's device, returning them in order ready for dispatch. Returns a failure
    /// result if device or shape constraints are violated (R15).
    /// </summary>
    /// <param name="a">First operand.</param>
    /// <param name="b">Second operand.</param>
    /// <param name="backend">The target backend.</param>
    /// <param name="requireSameShape">
    /// When <see langword="true"/>, shapes must be identical (e.g. for Add).
    /// When <see langword="false"/>, only device residency is verified (e.g. for MatMul,
    /// where shape validation is the backend's responsibility).
    /// </param>
    public static Result<(ITensor<float> A, ITensor<float> B), string> PrepareOperands(
        ITensor<float> a,
        ITensor<float> b,
        ITensorBackend backend,
        bool requireSameShape = false)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        ArgumentNullException.ThrowIfNull(backend);

        if (requireSameShape && !a.Shape.IsCompatibleWith(b.Shape))
            return Result<(ITensor<float>, ITensor<float>), string>.Failure(
                $"Shape mismatch: {a.Shape} vs {b.Shape}.");

        if (a.Device != backend.Device)
            return Result<(ITensor<float>, ITensor<float>), string>.Failure(
                $"Tensor A is on {a.Device} but backend targets {backend.Device}. " +
                $"Call TensorToBackendAdapter.EnsureDevice() before dispatching.");

        if (b.Device != backend.Device)
            return Result<(ITensor<float>, ITensor<float>), string>.Failure(
                $"Tensor B is on {b.Device} but backend targets {backend.Device}. " +
                $"Call TensorToBackendAdapter.EnsureDevice() before dispatching.");

        return Result<(ITensor<float>, ITensor<float>), string>.Success((a, b));
    }
}
