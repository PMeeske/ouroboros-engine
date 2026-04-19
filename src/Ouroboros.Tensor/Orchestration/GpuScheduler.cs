// <copyright file="GpuScheduler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// Priority for GPU-scheduled tasks. Higher priority tasks are dequeued first
/// and can interrupt backpressure waits.
/// </summary>
/// <remarks>
/// Phase 196.5 introduced <see cref="GpuPriorityClass"/> as the canonical priority model;
/// this enum is preserved for legacy call-site compatibility and maps directly into
/// <see cref="GpuPriorityClass"/> (see <see cref="GpuScheduler"/>).
/// </remarks>
public enum GpuTaskPriority
{
    /// <summary>Background jobs (batch processing, pre-computation).</summary>
    Background = 0,

    /// <summary>Normal inference tasks.</summary>
    Normal = 1,

    /// <summary>Interactive tasks requiring low latency (LLM responses). Maps to <see cref="GpuPriorityClass.Realtime"/>.</summary>
    High = 2,

    /// <summary>Real-time tasks (streaming video/audio processing).</summary>
    Realtime = 3,
}

/// <summary>
/// Describes the resource requirements of a GPU task for scheduling decisions.
/// </summary>
/// <param name="EstimatedVramBytes">Expected GPU memory consumption.</param>
/// <param name="RequiresExclusiveAccess">
/// If <see langword="true"/>, no other task will run concurrently.
/// Use for tasks that consume most of VRAM (e.g. LLM inference).
/// </param>
/// <param name="MaxLatency">
/// If set, the scheduler will reject the task with a timeout error if it
/// cannot be started within this window.
/// </param>
public readonly record struct GpuResourceRequirements(
    long EstimatedVramBytes,
    bool RequiresExclusiveAccess = false,
    TimeSpan? MaxLatency = null);

/// <summary>
/// Snapshot of GPU scheduler health, emitted as an observable for
/// dashboard integration and backpressure signalling.
/// </summary>
public readonly record struct GpuSchedulerMetrics(
    int QueueDepth,
    long EstimatedUsedVramBytes,
    long TotalVramBytes,
    double EstimatedUtilization,
    TimeSpan LastTaskLatency,
    long TotalTasksCompleted,
    long TotalTasksFailed);

/// <summary>
/// Legacy adapter over <see cref="GpuSchedulerV2"/> that preserves the pre-196.5
/// <c>ScheduleAsync(priority, requirements, work)</c> surface while routing all
/// execution through the new priority-preemptive scheduler.
/// </summary>
/// <remarks>
/// <para>
/// Each <see cref="GpuTaskPriority"/> tier is represented by a synthetic tenant
/// named <c>Legacy:{priority}</c> which is auto-registered on first use. The
/// VRAM overcommit check and <see cref="PauseAccepting"/>/<see cref="ResumeAccepting"/>
/// hooks remain here so existing callers (rasterizer, Kokoro, Avatar pipeline) stay green;
/// plan 03 (cooperative eviction) and plan 05 (Ollama / DXGI reconciliation) replace
/// those hooks with real eviction + budget enforcement.
/// </para>
/// <para>
/// TODO(196.5-03): remove when all callers migrate to <see cref="IGpuScheduler"/>.
/// </para>
/// </remarks>
public sealed class GpuScheduler : IDisposable
{
    private readonly GpuSchedulerV2 _v2;
    private readonly ConcurrentDictionary<GpuTaskPriority, IDisposable> _legacyRegistrations = new();
    private readonly long _totalVramBytes;
    private long _estimatedUsedVram;
    private volatile bool _paused;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="GpuScheduler"/>.
    /// </summary>
    /// <param name="totalVramBytes">
    /// Total GPU memory in bytes. Used for overcommit detection.
    /// </param>
    /// <param name="meter">Optional meter for OpenTelemetry integration.</param>
    public GpuScheduler(long totalVramBytes, Meter? meter = null)
    {
        _totalVramBytes = totalVramBytes;
        _v2 = new GpuSchedulerV2(totalVramBytes, meter);
    }

    /// <summary>Gets estimated available VRAM in bytes.</summary>
    public long EstimatedAvailableVram => _totalVramBytes - Interlocked.Read(ref _estimatedUsedVram);

    /// <summary>Gets the current queue depth.</summary>
    public int QueueDepth => _v2.CurrentMetrics.QueueDepth;

    /// <summary>Gets a snapshot of current scheduler metrics.</summary>
    public GpuSchedulerMetrics CurrentMetrics
    {
        get
        {
            var inner = _v2.CurrentMetrics;

            // Legacy adapter tracks estimatedUsedVram directly (its VRAM check is
            // still active); otherwise forward v2's metric shape.
            return inner with { EstimatedUsedVramBytes = Interlocked.Read(ref _estimatedUsedVram) };
        }
    }

    /// <summary>
    /// Pauses accepting new tasks. Existing queued tasks will still drain.
    /// </summary>
    public void PauseAccepting() => _paused = true;

    /// <summary>Resumes accepting new tasks after a pause.</summary>
    public void ResumeAccepting() => _paused = false;

    /// <summary>
    /// Schedules a GPU task with priority and resource requirements.
    /// </summary>
    /// <typeparam name="T">Return type of the GPU task.</typeparam>
    /// <param name="priority">Scheduling priority.</param>
    /// <param name="requirements">VRAM estimate and constraints.</param>
    /// <param name="work">The work to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of <paramref name="work"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the scheduler is paused due to memory pressure.
    /// </exception>
    /// <exception cref="InsufficientMemoryException">
    /// Thrown when estimated VRAM would exceed capacity for sub-High priority work.
    /// </exception>
    // TODO(196.5-03): remove when all callers migrate to IGpuScheduler.
    public async Task<T> ScheduleAsync<T>(
        GpuTaskPriority priority,
        GpuResourceRequirements requirements,
        Func<Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(work);

        if (_paused && priority < GpuTaskPriority.High)
        {
            throw new InvalidOperationException(
                "GPU scheduler is paused due to memory pressure. " +
                "Only High and Realtime priority tasks are accepted.");
        }

        // VRAM overcommit check (preserved from v1 — plan 03 replaces this with eviction).
        if (requirements.EstimatedVramBytes > EstimatedAvailableVram &&
            priority < GpuTaskPriority.High)
        {
            throw new InsufficientMemoryException(
                $"Estimated VRAM overcommit: task needs {requirements.EstimatedVramBytes} bytes, " +
                $"only {EstimatedAvailableVram} estimated available.");
        }

        var tenantName = EnsureLegacyTenant(priority);

        Interlocked.Add(ref _estimatedUsedVram, requirements.EstimatedVramBytes);
        try
        {
            var scheduled = _v2.ScheduleAsync(
                tenantName,
                async ct => await work().ConfigureAwait(false),
                cancellationToken);

            // Preserve legacy MaxLatency semantics: bail out even when the work is
            // still queued behind a long-running tenant. Plan 04 supersedes this with
            // per-tenant watchdog enforcement in the dispatch loop.
            if (requirements.MaxLatency is { } mx)
            {
                try
                {
                    return await scheduled.WaitAsync(mx, cancellationToken).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    throw new TimeoutException(
                        $"GPU task timed out waiting for device access after " +
                        $"{mx.TotalMilliseconds:F0}ms.");
                }
            }

            return await scheduled.ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Add(ref _estimatedUsedVram, -requirements.EstimatedVramBytes);
        }
    }

    /// <summary>
    /// Convenience overload for synchronous GPU work.
    /// </summary>
    /// <typeparam name="T">Return type of the work delegate.</typeparam>
    /// <param name="priority">Scheduling priority.</param>
    /// <param name="requirements">VRAM estimate and constraints.</param>
    /// <param name="work">Synchronous work delegate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of <paramref name="work"/>.</returns>
    // TODO(196.5-03): remove when all callers migrate to IGpuScheduler.
    public Task<T> ScheduleAsync<T>(
        GpuTaskPriority priority,
        GpuResourceRequirements requirements,
        Func<T> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        return ScheduleAsync(
            priority,
            requirements,
            () => Task.FromResult(work()),
            cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var reg in _legacyRegistrations.Values)
        {
            reg.Dispose();
        }

        _legacyRegistrations.Clear();
        _v2.Dispose();
    }

    private string EnsureLegacyTenant(GpuTaskPriority priority)
    {
        var name = $"Legacy:{priority}";
        if (_legacyRegistrations.ContainsKey(priority))
        {
            return name;
        }

        var profile = new GpuTenantProfile(
            TenantName: name,
            BasePriority: MapPriority(priority),
            VramBytes: 0,
            MaxDispatchTime: TimeSpan.FromMinutes(5),
            Preemptible: false,
            Eviction: EvictionPolicy.None);

        var registration = _v2.RegisterTenant(profile);
        if (!_legacyRegistrations.TryAdd(priority, registration))
        {
            // Lost the race — another thread registered first. Dispose ours.
            registration.Dispose();
        }

        return name;
    }

    private static GpuPriorityClass MapPriority(GpuTaskPriority p) => p switch
    {
        GpuTaskPriority.Background => GpuPriorityClass.Background,
        GpuTaskPriority.Normal => GpuPriorityClass.Normal,
        GpuTaskPriority.High => GpuPriorityClass.Realtime,
        GpuTaskPriority.Realtime => GpuPriorityClass.Realtime,
        _ => GpuPriorityClass.Normal,
    };
}
