// <copyright file="IPerceptionVramBudget.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// Admission-control registry consulted BEFORE any perception-tier ONNX session is
/// created. Each would-be session declares its estimated VRAM footprint; the budget
/// either grants a reservation or denies it, forcing the caller to surface a clear
/// failure instead of letting DirectML silently over-allocate.
/// </summary>
/// <remarks>
/// <para>
/// Phase 235 introduces this registry as the gate that closes requirement GPU-04.
/// Later phases (237+) that load YuNet, FER, SFace, MoveNet, MobileGaze etc. MUST
/// reserve here before calling <c>InferenceSession.Create</c>. Failing to do so is
/// a bug — there is no second line of defense for perception-tier over-allocation
/// because perception sits below Normal and Realtime in the scheduler.
/// </para>
/// <para>
/// The budget is flat (one total cap, one summed reservation count). It does not
/// model eviction policies — if a reservation is denied, the caller should log,
/// surface the failure, and skip the perception consumer until headroom recovers.
/// Pressure-driven sample-rate throttling (GPU-02) is handled separately by the
/// <c>AdaptiveSampleRateController</c> in the app layer.
/// </para>
/// </remarks>
public interface IPerceptionVramBudget
{
    /// <summary>Gets the total perception-tier VRAM budget in bytes.</summary>
    long TotalBudgetBytes { get; }

    /// <summary>Gets the currently reserved bytes across all live reservations.</summary>
    long ReservedBytes { get; }

    /// <summary>Gets the bytes still available for new reservations.</summary>
    long AvailableBytes { get; }

    /// <summary>
    /// Attempts to reserve <paramref name="estimatedBytes"/> for <paramref name="modelId"/>.
    /// On success, the returned <see cref="IVramReservation"/> holds the reservation until
    /// disposed. On failure, returns a <see cref="Result{T}"/> with a human-readable
    /// diagnostic listing current tenants.
    /// </summary>
    /// <param name="modelId">Stable identifier for the model (e.g. "yunet", "sface-128d").</param>
    /// <param name="estimatedBytes">Best-effort VRAM footprint estimate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see cref="Result{T}.Success(T)"/> carrying the live reservation, or
    /// <see cref="Result{T}.Failure(string)"/> when the reservation would exceed the budget.
    /// </returns>
    Task<Result<IVramReservation>> TryReserveAsync(
        string modelId,
        long estimatedBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns an immutable snapshot of every live reservation, for observability
    /// (metrics subscribers, <c>/api/perception/*</c> diagnostics).
    /// </summary>
    /// <returns>Immutable list of reservations, ordered by time reserved.</returns>
    IReadOnlyList<PerceptionVramReservation> Snapshot();
}

/// <summary>
/// Live handle on a VRAM reservation granted by <see cref="IPerceptionVramBudget"/>.
/// Disposing the handle releases the reservation back to the budget; the same handle
/// must never be disposed twice.
/// </summary>
public interface IVramReservation : IAsyncDisposable
{
    /// <summary>Gets the model identifier that owns this reservation.</summary>
    string ModelId { get; }

    /// <summary>Gets the reserved bytes.</summary>
    long Bytes { get; }
}

/// <summary>
/// Observable snapshot of a single live VRAM reservation.
/// </summary>
/// <param name="ModelId">Stable model identifier.</param>
/// <param name="Bytes">Bytes reserved.</param>
/// <param name="ReservedAt">UTC instant when the reservation was granted.</param>
public sealed record PerceptionVramReservation(
    string ModelId,
    long Bytes,
    DateTimeOffset ReservedAt);
