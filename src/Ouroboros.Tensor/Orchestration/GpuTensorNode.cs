// <copyright file="GpuTensorNode.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using R3;

namespace Ouroboros.Tensor.Orchestration;

/// <summary>
/// State of a GPU node in the reactive graph.
/// </summary>
public enum GpuNodeState
{
    /// <summary>Node is idle, waiting for input.</summary>
    Idle,

    /// <summary>Task is queued in the GPU scheduler.</summary>
    Queued,

    /// <summary>Task is executing on the GPU.</summary>
    Executing,

    /// <summary>Task completed with an error.</summary>
    Error,

    /// <summary>Node has been disposed.</summary>
    Disposed,
}

/// <summary>
/// A reactive node that processes <see cref="ITensor{T}"/> data on the GPU.
/// Subscribes to an input <see cref="IObservable{T}"/>, schedules GPU work through
/// the <see cref="GpuScheduler"/>, and pushes results to an output observable.
/// </summary>
/// <remarks>
/// <para>
/// This is the bridge between your reactive node graph (System.Reactive) and
/// the tensor pipeline (IAsyncEnumerable / Kleisli arrows). Each node wraps
/// a single GPU operation and participates in both worlds.
/// </para>
/// <para>
/// GPU work is serialised through the shared <see cref="GpuScheduler"/>.
/// Backpressure is applied via the scheduler's VRAM tracking — if the GPU is
/// overcommitted, lower-priority nodes will wait or fail gracefully.
/// </para>
/// <para>
/// The node uses <see cref="ITensorBackend"/> for actual compute, so it works
/// with any backend (ILGPU/OpenCL, TorchSharp, ONNX Runtime, CPU fallback).
/// </para>
/// </remarks>
public abstract class GpuTensorNode : IDisposable
{
    private readonly Subject<ITensor<float>> _output = new();
    private readonly Subject<GpuNodeState> _stateChanges = new();
    private DisposableBag _subscriptions;
    private GpuNodeState _state = GpuNodeState.Idle;

    /// <summary>Gets the shared GPU scheduler for priority and VRAM management.</summary>
    protected GpuScheduler Scheduler { get; }

    /// <summary>Gets the tensor backend to use for GPU operations.</summary>
    protected ITensorBackend Backend { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GpuTensorNode"/> class.
    /// Initializes a new <see cref="GpuTensorNode"/>.
    /// </summary>
    /// <param name="nodeId">Unique identifier for this node in the graph.</param>
    /// <param name="scheduler">Shared GPU scheduler instance.</param>
    /// <param name="backend">Tensor backend for compute operations.</param>
    /// <param name="priority">Default scheduling priority for this node's tasks.</param>
    protected GpuTensorNode(
        string nodeId,
        GpuScheduler scheduler,
        ITensorBackend backend,
        GpuTaskPriority priority = GpuTaskPriority.Normal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(backend);

        NodeId = nodeId;
        Scheduler = scheduler;
        Backend = backend;
        Priority = priority;
    }

    /// <summary>Gets the unique identifier of this node.</summary>
    public string NodeId { get; }

    /// <summary>Gets or sets the scheduling priority for this node's GPU tasks.</summary>
    public GpuTaskPriority Priority { get; set; }

    /// <summary>Gets the current state of this node.</summary>
    public GpuNodeState State
    {
        get => _state;
        private set
        {
            _state = value;
            _stateChanges.OnNext(value);
        }
    }

    /// <summary>Gets or sets the VRAM requirements for this node (override in subclasses).</summary>
    public virtual GpuResourceRequirements ResourceRequirements { get; protected set; }
        = new(EstimatedVramBytes: 64 * 1024 * 1024); // 64 MB default

    /// <summary>
    /// Gets observable output stream. Downstream nodes subscribe to this.
    /// </summary>
    public Observable<ITensor<float>> Output => _output;

    /// <summary>
    /// Gets observable state changes for monitoring/UI.
    /// </summary>
    public Observable<GpuNodeState> StateChanges => _stateChanges;

    /// <summary>
    /// Connects an upstream observable as the input to this node.
    /// Each emitted tensor is scheduled for GPU processing.
    /// </summary>
    /// <param name="source">The upstream tensor observable.</param>
    /// <returns>
    /// A disposable that, when disposed, disconnects this input source.
    /// Also stored internally and disposed when the node is disposed.
    /// </returns>
    public IDisposable SubscribeTo(Observable<ITensor<float>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var sub = source.Subscribe(
            tensor => _ = ProcessInputAsync(tensor),
            result => { if (result.IsSuccess)
{
    _output.OnCompleted();
}
            });

        sub.AddTo(ref _subscriptions);
        return sub;
    }

    /// <summary>
    /// Pushes a single tensor into this node for processing (imperative API).
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public Task ProcessAsync(ITensor<float> input, CancellationToken ct = default)
        => ProcessInputAsync(input, ct);

    /// <summary>
    /// Override this to implement the GPU operation.
    /// Called while holding the GPU scheduler lock.
    /// </summary>
    /// <param name="input">The input tensor (may be CPU or GPU resident).</param>
    /// <returns>
    /// <see cref="Result{TSuccess,TError}.Success"/> with the output tensor, or
    /// <see cref="Result{TSuccess,TError}.Failure"/> with an error message.
    /// </returns>
    protected abstract Result<ITensor<float>, string> Execute(ITensor<float> input);

    private async Task ProcessInputAsync(
        ITensor<float> input,
        CancellationToken ct = default)
    {
        try
        {
            State = GpuNodeState.Queued;

            var result = await Scheduler.ScheduleAsync(
                Priority,
                ResourceRequirements,
                () =>
                {
                    State = GpuNodeState.Executing;
                    return Execute(input);
                },
                ct).ConfigureAwait(false);

            State = GpuNodeState.Idle;

            if (result.IsSuccess)
            {
                _output.OnNext(result.Value);
            }
            else
            {
                State = GpuNodeState.Error;
                _output.OnErrorResume(new InvalidOperationException(
                    $"GPU node '{NodeId}' failed: {result.Error}"));
            }
        }
        catch (InvalidOperationException ex)
        {
            State = GpuNodeState.Error;
            _output.OnErrorResume(ex);
        }
    }

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        State = GpuNodeState.Disposed;
        _subscriptions.Dispose();
        _output.OnCompleted();
        _output.Dispose(false);
        _stateChanges.OnCompleted();
        _stateChanges.Dispose(false);
    }
}

// ─── Concrete Node Implementations ──────────────────────────────────────────

/// <summary>
/// GPU node that applies an <see cref="ITensorBackend.MatMul"/> operation.
/// Useful for linear layers, attention, and projection in inference pipelines.
/// </summary>
public sealed class MatMulNode : GpuTensorNode
{
    private readonly ITensor<float> _weights;

    /// <summary>
    /// Initializes a new instance of the <see cref="MatMulNode"/> class.
    /// Creates a MatMul node with fixed weights (e.g. a model layer).
    /// </summary>
    /// <param name="nodeId">Unique node identifier.</param>
    /// <param name="scheduler">GPU scheduler.</param>
    /// <param name="backend">Tensor backend.</param>
    /// <param name="weights">Weight matrix (will be kept alive for the node's lifetime).</param>
    public MatMulNode(
        string nodeId,
        GpuScheduler scheduler,
        ITensorBackend backend,
        ITensor<float> weights)
        : base(nodeId, scheduler, backend)
    {
        _weights = weights;
        ResourceRequirements = new(
            EstimatedVramBytes: weights.Shape.ElementCount * sizeof(float) * 3);
    }

    /// <inheritdoc/>
    protected override Result<ITensor<float>, string> Execute(ITensor<float> input)
        => Backend.MatMul(input, _weights);

    /// <inheritdoc/>
    public override void Dispose()
    {
        _weights.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// GPU node that runs ONNX model inference using the
/// <see cref="OnnxRuntimeTensorBackend.RunInference"/> API.
/// </summary>
public sealed class OnnxInferenceNode : GpuTensorNode
{
    private readonly Backends.OnnxRuntimeTensorBackend _onnxBackend;
    private readonly Microsoft.ML.OnnxRuntime.InferenceSession _session;
    private readonly string _inputName;
    private readonly string _outputName;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnnxInferenceNode"/> class.
    /// Creates an ONNX inference node.
    /// </summary>
    /// <param name="nodeId">Unique node identifier.</param>
    /// <param name="scheduler">GPU scheduler.</param>
    /// <param name="onnxBackend">ONNX Runtime tensor backend.</param>
    /// <param name="modelPath">Path to the .onnx model file.</param>
    /// <param name="inputName">Name of the model's input tensor.</param>
    /// <param name="outputName">Name of the model's output tensor to extract.</param>
    public OnnxInferenceNode(
        string nodeId,
        GpuScheduler scheduler,
        Backends.OnnxRuntimeTensorBackend onnxBackend,
        string modelPath,
        string inputName = "input",
        string outputName = "output")
        : base(nodeId, scheduler, onnxBackend, GpuTaskPriority.Normal)
    {
        _onnxBackend = onnxBackend;
        _inputName = inputName;
        _outputName = outputName;

        using var opts = new Microsoft.ML.OnnxRuntime.SessionOptions();

        // AMD ROCm: use MIGraphX or DirectML EP depending on platform
        // opts.AppendExecutionProvider_MIGraphX();
        _session = new Microsoft.ML.OnnxRuntime.InferenceSession(modelPath, opts);

        var fileSize = new FileInfo(modelPath).Length;
        ResourceRequirements = new(EstimatedVramBytes: fileSize * 3);
    }

    /// <inheritdoc/>
    protected override Result<ITensor<float>, string> Execute(ITensor<float> input)
    {
        var cpuInput = input.Device == DeviceType.Cpu ? input : input.ToCpu();
        var inputs = new Dictionary<string, ITensor<float>> { [_inputName] = cpuInput };

        var result = _onnxBackend.RunInference(_session, inputs, [_outputName]);
        if (result.IsFailure)
        {
            return Result<ITensor<float>, string>.Failure(result.Error);
        }

        if (result.Value.TryGetValue(_outputName, out var output))
        {
            return Result<ITensor<float>, string>.Success(output);
        }

        return Result<ITensor<float>, string>.Failure(
            $"Output '{_outputName}' not found in ONNX inference results.");
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _session.Dispose();
        base.Dispose();
    }
}

#if ENABLE_ILGPU

/// <summary>
/// GPU node that runs a custom ILGPU kernel via
/// <see cref="IlgpuOpenClTensorBackend.RunKernel"/>.
/// Implement a C# kernel and it will be JIT-compiled to OpenCL for AMD GPUs.
/// </summary>
public sealed class IlgpuKernelNode : GpuTensorNode
{
    private readonly Func<ILGPU.Runtime.Accelerator,
        Action<ILGPU.ArrayView<float>, ILGPU.ArrayView<float>>> _kernelFactory;
    private readonly Func<TensorShape, TensorShape> _outputShapeFactory;

    /// <summary>
    /// Creates an ILGPU kernel node.
    /// </summary>
    /// <param name="nodeId">Unique node identifier.</param>
    /// <param name="scheduler">GPU scheduler.</param>
    /// <param name="backend">ILGPU OpenCL backend.</param>
    /// <param name="kernelFactory">
    /// Factory that creates the kernel delegate from the accelerator.
    /// The delegate receives (inputView, outputView).
    /// </param>
    /// <param name="outputShapeFactory">
    /// Computes the output shape from the input shape.
    /// Return the same shape for element-wise operations.
    /// </param>
    public IlgpuKernelNode(
        string nodeId,
        GpuScheduler scheduler,
        Backends.IlgpuOpenClTensorBackend backend,
        Func<ILGPU.Runtime.Accelerator,
            Action<ILGPU.ArrayView<float>, ILGPU.ArrayView<float>>> kernelFactory,
        Func<TensorShape, TensorShape>? outputShapeFactory = null)
        : base(nodeId, scheduler, backend)
    {
        _kernelFactory = kernelFactory;
        _outputShapeFactory = outputShapeFactory ?? (shape => shape); // identity
    }

    /// <inheritdoc/>
    protected override Result<ITensor<float>, string> Execute(ITensor<float> input)
    {
        if (Backend is not Backends.IlgpuOpenClTensorBackend ilgpuBackend)
            return Result<ITensor<float>, string>.Failure(
                "IlgpuKernelNode requires an IlgpuOpenClTensorBackend.");

        var outputShape = _outputShapeFactory(input.Shape);
        return ilgpuBackend.RunKernel(input, outputShape, _kernelFactory);
    }
}

#endif // ENABLE_ILGPU
