// <copyright file="PerceptionVramBudget.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// Default <see cref="IPerceptionVramBudget"/> implementation. Tracks a flat byte
/// budget protected by a <see cref="SemaphoreSlim"/> so concurrent model-load
/// attempts cannot double-book the same headroom.
/// </summary>
public sealed class PerceptionVramBudget : IPerceptionVramBudget, IDisposable
{
    private readonly long _totalBudgetBytes;
    private readonly ILogger<PerceptionVramBudget> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<LiveReservation> _live = new();
    private long _reservedBytes;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="PerceptionVramBudget"/> class.</summary>
    /// <param name="totalBudgetBytes">Total perception-tier VRAM budget in bytes.</param>
    /// <param name="logger">Structured logger.</param>
    public PerceptionVramBudget(long totalBudgetBytes, ILogger<PerceptionVramBudget> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (totalBudgetBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalBudgetBytes),
                totalBudgetBytes,
                "Perception VRAM budget must be positive.");
        }

        _totalBudgetBytes = totalBudgetBytes;
        _logger = logger;
        _logger.LogInformation(
            "[PerceptionVramBudget] initialised with {BudgetMb} MB",
            totalBudgetBytes / (1024 * 1024));
    }

    /// <inheritdoc/>
    public long TotalBudgetBytes => _totalBudgetBytes;

    /// <inheritdoc/>
    public long ReservedBytes => Interlocked.Read(ref _reservedBytes);

    /// <inheritdoc/>
    public long AvailableBytes => _totalBudgetBytes - Interlocked.Read(ref _reservedBytes);

    /// <inheritdoc/>
    public async Task<Result<IVramReservation>> TryReserveAsync(
        string modelId,
        long estimatedBytes,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        if (estimatedBytes <= 0)
        {
            return Result<IVramReservation>.Failure(
                $"estimatedBytes must be positive, got {estimatedBytes} for '{modelId}'.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            long newTotal = _reservedBytes + estimatedBytes;
            if (newTotal > _totalBudgetBytes)
            {
                string tenants = _live.Count == 0
                    ? "(none)"
                    : string.Join(", ", _live.ConvertAll(r => $"{r.ModelId}={r.Bytes / (1024 * 1024)}MB"));
                string msg =
                    $"Perception VRAM budget denial for '{modelId}': " +
                    $"need {estimatedBytes / (1024 * 1024)}MB, " +
                    $"available {AvailableBytes / (1024 * 1024)}MB of " +
                    $"{_totalBudgetBytes / (1024 * 1024)}MB total. " +
                    $"Live tenants: {tenants}.";
                _logger.LogWarning("[PerceptionVramBudget] {Message}", msg);
                return Result<IVramReservation>.Failure(msg);
            }

            var live = new LiveReservation(this, modelId, estimatedBytes, DateTimeOffset.UtcNow);
            _live.Add(live);
            Interlocked.Add(ref _reservedBytes, estimatedBytes);

            _logger.LogInformation(
                "[PerceptionVramBudget] reserved {Mb}MB for '{ModelId}' (total {Used}/{Cap}MB, {Count} tenant(s))",
                estimatedBytes / (1024 * 1024),
                modelId,
                _reservedBytes / (1024 * 1024),
                _totalBudgetBytes / (1024 * 1024),
                _live.Count);

            return Result<IVramReservation>.Success(live);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<PerceptionVramReservation> Snapshot()
    {
        _gate.Wait();
        try
        {
            var copy = new List<PerceptionVramReservation>(_live.Count);
            foreach (var r in _live)
            {
                copy.Add(new PerceptionVramReservation(r.ModelId, r.Bytes, r.ReservedAt));
            }

            return copy;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gate.Dispose();
    }

    private async ValueTask ReleaseAsync(LiveReservation reservation)
    {
        if (_disposed)
        {
            return;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_live.Remove(reservation))
            {
                // Already released (double dispose); nothing to do.
                return;
            }

            Interlocked.Add(ref _reservedBytes, -reservation.Bytes);
            _logger.LogInformation(
                "[PerceptionVramBudget] released {Mb}MB for '{ModelId}' (total {Used}/{Cap}MB, {Count} tenant(s))",
                reservation.Bytes / (1024 * 1024),
                reservation.ModelId,
                _reservedBytes / (1024 * 1024),
                _totalBudgetBytes / (1024 * 1024),
                _live.Count);
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed class LiveReservation : IVramReservation
    {
        private readonly PerceptionVramBudget _owner;
        private int _released;

        public LiveReservation(PerceptionVramBudget owner, string modelId, long bytes, DateTimeOffset reservedAt)
        {
            _owner = owner;
            ModelId = modelId;
            Bytes = bytes;
            ReservedAt = reservedAt;
        }

        public string ModelId { get; }

        public long Bytes { get; }

        public DateTimeOffset ReservedAt { get; }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
            {
                return;
            }

            await _owner.ReleaseAsync(this).ConfigureAwait(false);
        }
    }
}
