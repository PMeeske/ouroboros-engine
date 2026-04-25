// <copyright file="OnnxRuntimeTensorBackend.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Ouroboros.Tensor.Abstractions;

namespace Ouroboros.Tensor.Backends;

/// <summary>
/// An <see cref="ITensorBackend"/> adapter for running ONNX models via
/// <c>Microsoft.ML.OnnxRuntime</c>. Intended for inference scenarios where a pre-trained
/// ONNX model is the execution unit rather than generic tensor arithmetic (R02, R13).
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="CpuTensorBackend"/>, this backend is primarily a data conversion layer:
/// <see cref="Create"/> and <see cref="FromMemory"/> produce tensors compatible with ONNX
/// Runtime session inputs, while <see cref="RunInference"/> executes a model and returns
/// the outputs as <see cref="ITensor{T}"/> instances.
/// </para>
/// <para>
/// <see cref="MatMul"/> and <see cref="Add"/> delegate to <see cref="CpuTensorBackend"/>
/// for general-purpose arithmetic so that pre/post-processing steps can share the same
/// backend interface. Callers that need GPU-accelerated ONNX inference should use
/// <c>ExecutionProviders</c> (CUDA EP) via <see cref="SessionOptions"/>.
/// </para>
/// <para>
/// Phase 196.3: when constructed with an <see cref="ISharedOrtDmlSessionFactory"/>,
/// the backend can produce factory-bound <see cref="SessionOptions"/> via
/// <see cref="TryCreateSessionOptions"/> so that callers creating
/// <see cref="InferenceSession"/> instances route through the shared D3D12 device.
/// </para>
/// </remarks>
public sealed class OnnxRuntimeTensorBackend : ITensorBackend, IDisposable
{
    // Per-session circuit breaker. When the same InferenceSession throws
    // OnnxRuntimeException FailureThreshold times in a row, we stop calling
    // session.Run for CooldownSeconds and return Failure synthetically. This
    // prevents a 30-60 fps avatar render loop from amplifying a deterministic
    // ORT failure into 70-200 exceptions/sec, each carrying a stack-trace string
    // that promotes to LOH/gen2 and never gets reclaimed.
    private const int FailureThreshold = 10;
    private const int CooldownSeconds = 30;

    private static readonly ConditionalWeakTable<InferenceSession, CircuitState> S_circuits = new();

    private sealed class CircuitState
    {
        public int ConsecutiveFailures;
        public long OpenUntilTicks; // 0 = closed, otherwise UTC ticks past which we retry
        public string? LastMessage;
    }

    private readonly CpuTensorBackend _cpuFallback = CpuTensorBackend.Instance;
    private readonly ISharedOrtDmlSessionFactory? _sessionFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnnxRuntimeTensorBackend"/> class.
    /// Initializes a new <see cref="OnnxRuntimeTensorBackend"/> with optional session factory.
    /// Pass a configured <see cref="ISharedOrtDmlSessionFactory"/> to enable shared D3D12
    /// DirectML sessions.
    /// </summary>
    public OnnxRuntimeTensorBackend(ISharedOrtDmlSessionFactory? sessionFactory = null)
    {
        _sessionFactory = sessionFactory;
    }

    /// <inheritdoc/>
    /// <remarks>Returns <see cref="Abstractions.DeviceType.Cpu"/> for the CPU ONNX EP.</remarks>
    public DeviceType Device => DeviceType.Cpu;

    /// <inheritdoc/>
    public ITensor<float> Create(TensorShape shape, ReadOnlySpan<float> data)
        => _cpuFallback.Create(shape, data);

    /// <inheritdoc/>
    public ITensor<float> CreateUninitialized(TensorShape shape)
        => _cpuFallback.CreateUninitialized(shape);

    /// <inheritdoc/>
    public ITensor<float> FromMemory(ReadOnlyMemory<float> memory, TensorShape shape)
        => _cpuFallback.FromMemory(memory, shape);

    /// <inheritdoc/>
    public Result<ITensor<float>, string> MatMul(ITensor<float> a, ITensor<float> b)
        => _cpuFallback.MatMul(a, b);

    /// <inheritdoc/>
    public Result<ITensor<float>, string> Add(ITensor<float> a, ITensor<float> b)
        => _cpuFallback.Add(a, b);

    /// <summary>
    /// Runs an ONNX model session against a set of named input tensors and returns the
    /// named output tensors. This is the primary API for inference use cases (R13).
    /// </summary>
    /// <param name="session">The ONNX inference session.</param>
    /// <param name="inputs">
    /// Named input map. Keys must match the session's input names.
    /// </param>
    /// <param name="outputNames">
    /// Names of the outputs to collect. When null, all outputs are returned.
    /// </param>
    /// <returns>
    /// A dictionary mapping output name to tensor on success,
    /// or a failure with an error description.
    /// </returns>
    public Result<IReadOnlyDictionary<string, ITensor<float>>, string> RunInference(
        InferenceSession session,
        IReadOnlyDictionary<string, ITensor<float>> inputs,
        IEnumerable<string>? outputNames = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(inputs);

        CircuitState circuit = S_circuits.GetValue(session, _ => new CircuitState());

        long openUntil = Volatile.Read(ref circuit.OpenUntilTicks);
        if (openUntil != 0)
        {
            if (DateTime.UtcNow.Ticks < openUntil)
            {
                return Result<IReadOnlyDictionary<string, ITensor<float>>, string>.Failure(
                    $"ONNX session circuit open: {circuit.LastMessage}");
            }

            // Cooldown elapsed — half-open: reset the counter and try once.
            Interlocked.Exchange(ref circuit.OpenUntilTicks, 0L);
            Interlocked.Exchange(ref circuit.ConsecutiveFailures, 0);
        }

        try
        {
            // Convert ITensor<float> → OnnxValue
            var onnxInputs = new List<NamedOnnxValue>(inputs.Count);
            foreach (var (name, tensor) in inputs)
            {
                var dims = tensor.Shape.Dimensions.Select(d => (long)d).ToArray();
                var ortTensor = new DenseTensor<float>(
                    MemoryMarshal.AsMemory(tensor.AsMemory()),
                    dims.Select(d => (int)d).ToArray());
                onnxInputs.Add(NamedOnnxValue.CreateFromTensor(name, ortTensor));
            }

            var requestedOutputs = outputNames?.ToList();
            using var results = requestedOutputs is null
                ? session.Run(onnxInputs)
                : session.Run(onnxInputs, requestedOutputs);

            var outputs = new Dictionary<string, ITensor<float>>(results.Count);
            foreach (var output in results)
            {
                if (output.Value is DenseTensor<float> denseTensor)
                {
                    var shape = TensorShape.Of(denseTensor.Dimensions.ToArray());
                    var data = denseTensor.Buffer.ToArray();
                    outputs[output.Name] = _cpuFallback.Create(shape, data.AsSpan());
                }
            }

            // Success — reset circuit.
            Interlocked.Exchange(ref circuit.ConsecutiveFailures, 0);
            return Result<IReadOnlyDictionary<string, ITensor<float>>, string>.Success(outputs);
        }
        catch (OnnxRuntimeException ex)
        {
            return RecordFailure(circuit, $"ONNX inference failed: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return RecordFailure(circuit, $"Unexpected error during ONNX inference: {ex.Message}");
        }
    }

    private static Result<IReadOnlyDictionary<string, ITensor<float>>, string> RecordFailure(
        CircuitState circuit, string message)
    {
        circuit.LastMessage = message;
        int count = Interlocked.Increment(ref circuit.ConsecutiveFailures);
        if (count >= FailureThreshold)
        {
            long until = DateTime.UtcNow.AddSeconds(CooldownSeconds).Ticks;

            // Only the first thread that trips the breaker emits the trace, to avoid log spam.
            if (Interlocked.CompareExchange(ref circuit.OpenUntilTicks, until, 0L) == 0L)
            {
                Trace.TraceError(
                    "[ONNX] Circuit breaker opened after {0} consecutive failures (cooldown {1}s): {2}",
                    count, CooldownSeconds, message);
            }
        }

        return Result<IReadOnlyDictionary<string, ITensor<float>>, string>.Failure(message);
    }

    /// <summary>
    /// When a factory was supplied at construction, returns a
    /// <see cref="SessionOptions"/> bound to the shared D3D12 device.
    /// Otherwise returns <c>null</c> so the caller can fall back to CPU.
    /// </summary>
    /// <returns></returns>
    public SessionOptions? TryCreateSessionOptions()
    {
        if (_sessionFactory is null)
        {
            return null;
        }

        try
        {
            return _sessionFactory.CreateSessionOptions();
        }
        catch (InvalidOperationException)
        {
            // Shared device unavailable — caller must fall back to CPU EP
            return null;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}
