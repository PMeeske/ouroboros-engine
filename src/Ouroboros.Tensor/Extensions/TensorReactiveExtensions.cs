// <copyright file="TensorReactiveExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using R3;

namespace Ouroboros.Tensor.Extensions;

/// <summary>
/// Bridges the <see cref="IAsyncEnumerable{T}"/>-based tensor pipeline (Kleisli arrows)
/// with the <see cref="IObservable{T}"/>-based reactive node graph. Enables composing
/// streaming tensor pipelines as reactive nodes without changing either abstraction.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ToObservable{T}"/> converts an async stream into a cold observable
/// that materialises elements on subscription. <see cref="ToAsyncEnumerable{T}"/>
/// converts an observable back to an async stream for pipeline stages that expect it.
/// </para>
/// <para>
/// <see cref="ScheduleOnGpu"/> injects the <see cref="GpuScheduler"/> into an
/// observable pipeline, ensuring each tensor is processed under the GPU lock with
/// proper priority and VRAM accounting.
/// </para>
/// </remarks>
public static class TensorReactiveExtensions
{
    // ── IAsyncEnumerable → IObservable ──────────────────────────────────────

    /// <summary>
    /// Converts an <see cref="IAsyncEnumerable{T}"/> of tensors into a cold
    /// <see cref="IObservable{T}"/>. Each subscription starts consuming the stream.
    /// </summary>
    /// <typeparam name="T">Element type (typically <see cref="ITensor{T}"/>).</typeparam>
    /// <param name="source">The async tensor stream.</param>
    /// <returns>A cold observable that emits each tensor from the stream.</returns>
    public static Observable<T> ToObservable<T>(this IAsyncEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return Observable.Create<T>(async (observer, ct) =>
        {
            try
            {
                await foreach (var item in source.WithCancellation(ct).ConfigureAwait(false))
                {
                    observer.OnNext(item);
                }

                observer.OnCompleted();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Normal cancellation on unsubscribe — do not propagate
            }
            catch (InvalidOperationException ex)
            {
                observer.OnErrorResume(ex);
            }
        });
    }

    // ── IObservable → IAsyncEnumerable ──────────────────────────────────────

    /// <summary>
    /// Converts an <see cref="IObservable{T}"/> back to an
    /// <see cref="IAsyncEnumerable{T}"/> for interop with pipeline arrows.
    /// </summary>
    /// <remarks>
    /// Uses a bounded channel (capacity 128, DropOldest) to prevent unbounded
    /// memory growth when the observable emits faster than the consumer iterates.
    /// </remarks>
    /// <returns></returns>
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(
        this Observable<T> source,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var channel = System.Threading.Channels.Channel.CreateBounded<T>(
            new System.Threading.Channels.BoundedChannelOptions(128)
            {
                FullMode = System.Threading.Channels.BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true,
            });

        using var sub = source.Subscribe(
            item => channel.Writer.TryWrite(item),
            result => { if (result.IsSuccess)
{
    channel.Writer.Complete();
}
            });

        await foreach (var item in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    // ── GPU Scheduling Operator ─────────────────────────────────────────────

    /// <summary>
    /// Injects GPU scheduling into a tensor observable pipeline. Each tensor
    /// is processed through <paramref name="gpuWork"/> under the
    /// <see cref="GpuScheduler"/> lock with the specified priority.
    /// </summary>
    /// <param name="source">Input tensor stream.</param>
    /// <param name="scheduler">GPU scheduler for serialisation and VRAM tracking.</param>
    /// <param name="priority">Scheduling priority for this pipeline stage.</param>
    /// <param name="requirements">VRAM requirements per invocation.</param>
    /// <param name="gpuWork">
    /// The GPU operation to perform on each tensor. Runs while holding the GPU lock.
    /// </param>
    /// <returns>An observable of processed output tensors.</returns>
    public static Observable<ITensor<float>> ScheduleOnGpu(
        this Observable<ITensor<float>> source,
        GpuScheduler scheduler,
        GpuTaskPriority priority,
        GpuResourceRequirements requirements,
        Func<ITensor<float>, Result<ITensor<float>, string>> gpuWork)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(gpuWork);

        return source.SelectAwait(async (input, ct) =>
        {
            var result = await scheduler.ScheduleAsync(
                priority,
                requirements,
                () => gpuWork(input),
                ct).ConfigureAwait(false);

            if (result.IsFailure)
            {
                throw new InvalidOperationException(
                    $"GPU operation failed: {result.Error}");
            }

            return result.Value;
        });
    }

    // ── Node Graph Wiring Helpers ───────────────────────────────────────────

    /// <summary>
    /// Connects two <see cref="GpuTensorNode"/>s: the output of
    /// <paramref name="upstream"/> feeds into <paramref name="downstream"/>.
    /// </summary>
    /// <returns>A disposable that disconnects the link when disposed.</returns>
    public static IDisposable ConnectTo(
        this GpuTensorNode upstream,
        GpuTensorNode downstream)
    {
        ArgumentNullException.ThrowIfNull(upstream);
        ArgumentNullException.ThrowIfNull(downstream);

        return downstream.SubscribeTo(upstream.Output);
    }

    /// <summary>
    /// Forks a node's output to multiple downstream nodes.
    /// </summary>
    /// <returns>A composite disposable that disconnects all links.</returns>
    public static IDisposable ForkTo(
        this GpuTensorNode upstream,
        params GpuTensorNode[] downstreams)
    {
        DisposableBag disposables = default;
        foreach (var ds in downstreams)
        {
            upstream.ConnectTo(ds).AddTo(ref disposables);
        }

        var bag = disposables;
        return Disposable.Create(() => bag.Dispose());
    }

    /// <summary>
    /// Merges outputs from multiple upstream nodes into a single observable,
    /// optionally concatenating the tensors along a batch dimension.
    /// </summary>
    /// <returns></returns>
    public static Observable<ITensor<float>> MergeOutputs(
        params GpuTensorNode[] nodes)
    {
        return nodes.Select(n => n.Output).Merge();
    }

    // ── Pipeline Arrow Bridge ───────────────────────────────────────────────

    /// <summary>
    /// Adapts a Kleisli arrow pipeline stage into a GPU-scheduled reactive operator.
    /// This lets you reuse existing <see cref="TensorPipelineArrows"/> stages
    /// inside the reactive node graph.
    /// </summary>
    /// <param name="source">Input observable.</param>
    /// <param name="stage">
    /// An async pipeline stage (e.g. from <see cref="TensorPipelineArrows"/>).
    /// </param>
    /// <returns>Flattened output observable.</returns>
    public static Observable<ITensor<float>> ThroughPipelineStage(
        this Observable<IAsyncEnumerable<float[]>> source,
        Step<IAsyncEnumerable<float[]>, IAsyncEnumerable<ITensor<float>>> stage)
    {
        return source.SelectAwait(async (batch, ct) => await stage(batch).ConfigureAwait(false))
            .SelectMany(stream => stream.ToObservable());
    }
}
