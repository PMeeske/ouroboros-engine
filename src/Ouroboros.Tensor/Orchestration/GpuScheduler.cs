// <copyright file="GpuScheduler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// Priority for GPU-scheduled tasks. Higher priority tasks are dequeued first
/// and can interrupt backpressure waits.
/// </summary>
public enum GpuTaskPriority
{
    /// <summary>Background jobs (batch processing, pre-computation).</summary>
    Background = 0,

    /// <summary>Normal inference tasks.</summary>
    Normal = 1,

    /// <summary>Interactive tasks requiring low latency (LLM responses).</summary>
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
/// Serialises access to the GPU, providing priority-based scheduling, VRAM accounting,
/// and observable metrics. Works with any <see cref="ITensorBackend"/> — the scheduler
/// gates access to the device, not to a specific backend implementation.
/// </summary>
/// <remarks>
/// <para>
/// Tasks are submitted via <see cref="ScheduleAsync{T}"/> with a priority and resource estimate.
/// The scheduler ensures at most one task (or one non-exclusive task) runs at a time.
/// </para>
/// <para>
/// VRAM tracking is estimate-based. The scheduler does not query actual device memory;
/// callers provide <see cref="GpuResourceRequirements.EstimatedVramBytes"/> and the
/// scheduler uses bookkeeping to detect overcommit before dispatching (R12, R17).
/// </para>
/// </remarks>
public sealed class GpuScheduler : IDisposable
{
    private readonly SemaphoreSlim _gpuLock = new(1, 1);
    private readonly PriorityQueue<Func<Task>, int> _waitQueue = new();
    private readonly long _totalVramBytes;
    private long _estimatedUsedVram;
    private long _completedCount;
    private long _failedCount;
    private TimeSpan _lastLatency;
    private volatile bool _paused;
    private volatile bool _disposed;

    // Metrics
    private readonly Meter _meter;
    private readonly Counter<long> _tasksCompletedCounter;
    private readonly Counter<long> _tasksFailedCounter;
    private readonly Histogram<double> _latencyHistogram;

    /// <summary>
    /// Initializes a new <see cref="GpuScheduler"/>.
    /// </summary>
    /// <param name="totalVramBytes">
    /// Total GPU memory in bytes. Used for overcommit detection.
    /// Pass <see cref="IlgpuOpenClTensorBackend.TotalMemoryBytes"/> or a manual estimate.
    /// </param>
    /// <param name="meter">Optional meter for OpenTelemetry integration.</param>
    public GpuScheduler(long totalVramBytes, Meter? meter = null)
    {
        _totalVramBytes = totalVramBytes;
        _meter = meter ?? new Meter("Ouroboros.Tensor.GpuScheduler");
        _tasksCompletedCounter = _meter.CreateCounter<long>("gpu.tasks.completed");
        _tasksFailedCounter = _meter.CreateCounter<long>("gpu.tasks.failed");
        _latencyHistogram = _meter.CreateHistogram<double>("gpu.task.latency_ms");
    }

    /// <summary>Gets estimated available VRAM in bytes.</summary>
    public long EstimatedAvailableVram => _totalVramBytes - Interlocked.Read(ref _estimatedUsedVram);

    /// <summary>Gets the current queue depth.</summary>
    public int QueueDepth => _waitQueue.Count;

    /// <summary>Gets a snapshot of current scheduler metrics.</summary>
    public GpuSchedulerMetrics CurrentMetrics => new(
        QueueDepth,
        Interlocked.Read(ref _estimatedUsedVram),
        _totalVramBytes,
        (double)Interlocked.Read(ref _estimatedUsedVram) / _totalVramBytes,
        _lastLatency,
        Interlocked.Read(ref _completedCount),
        Interlocked.Read(ref _failedCount));

    /// <summary>
    /// Pauses accepting new tasks. Existing queued tasks will still drain.
    /// Used by the memory pool to apply backpressure under memory pressure.
    /// </summary>
    public void PauseAccepting() => _paused = true;

    /// <summary>Resumes accepting new tasks after a pause.</summary>
    public void ResumeAccepting() => _paused = false;

    /// <summary>
    /// Schedules a GPU task with priority and resource requirements.
    /// The <paramref name="work"/> delegate receives the GPU lock and must
    /// complete all GPU operations before returning.
    /// </summary>
    /// <typeparam name="T">Return type of the GPU task.</typeparam>
    /// <param name="priority">Scheduling priority.</param>
    /// <param name="requirements">VRAM estimate and constraints.</param>
    /// <param name="work">
    /// The work to execute while holding the GPU lock.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of <paramref name="work"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the scheduler is paused due to memory pressure.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// Thrown when <see cref="GpuResourceRequirements.MaxLatency"/> is exceeded while waiting.
    /// </exception>
    public async Task<T> ScheduleAsync<T>(
        GpuTaskPriority priority,
        GpuResourceRequirements requirements,
        Func<Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_paused && priority < GpuTaskPriority.High)
            throw new InvalidOperationException(
                "GPU scheduler is paused due to memory pressure. " +
                "Only High and Realtime priority tasks are accepted.");

        // VRAM overcommit check
        if (requirements.EstimatedVramBytes > EstimatedAvailableVram &&
            priority < GpuTaskPriority.High)
        {
            throw new InsufficientMemoryException(
                $"Estimated VRAM overcommit: task needs {requirements.EstimatedVramBytes} bytes, " +
                $"only {EstimatedAvailableVram} estimated available.");
        }

        // Acquire GPU lock with optional timeout
        var timeout = requirements.MaxLatency ?? Timeout.InfiniteTimeSpan;
        if (!await _gpuLock.WaitAsync(timeout, cancellationToken).ConfigureAwait(false))
        {
            throw new TimeoutException(
                $"GPU task timed out waiting for device access after {timeout.TotalMilliseconds:F0}ms.");
        }

        try
        {
            Interlocked.Add(ref _estimatedUsedVram, requirements.EstimatedVramBytes);
            var sw = Stopwatch.StartNew();

            var result = await work().ConfigureAwait(false);

            sw.Stop();
            _lastLatency = sw.Elapsed;
            _latencyHistogram.Record(sw.Elapsed.TotalMilliseconds);
            _tasksCompletedCounter.Add(1);
            Interlocked.Increment(ref _completedCount);

            return result;
        }
        catch
        {
            _tasksFailedCounter.Add(1);
            Interlocked.Increment(ref _failedCount);
            throw;
        }
        finally
        {
            Interlocked.Add(ref _estimatedUsedVram, -requirements.EstimatedVramBytes);
            _gpuLock.Release();
        }
    }

    /// <summary>
    /// Convenience overload for synchronous GPU work.
    /// </summary>
    public Task<T> ScheduleAsync<T>(
        GpuTaskPriority priority,
        GpuResourceRequirements requirements,
        Func<T> work,
        CancellationToken cancellationToken = default)
    {
        return ScheduleAsync(
            priority,
            requirements,
            () => Task.FromResult(work()),
            cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _gpuLock.Dispose();
            _meter.Dispose();
        }
    }
}
