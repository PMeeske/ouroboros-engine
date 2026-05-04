// <copyright file="OnnxTensorBackend.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.OnnxRuntime;
using Ouroboros.Tensor.Abstractions;

namespace Ouroboros.Tensor.Backends;

/// <summary>
/// First-class ONNX Runtime + DirectML tensor backend. The default ITensorBackend
/// for hosts that have a shared D3D12 device (Windows + DirectX 12 GPU) — composes
/// <see cref="OnnxRuntimeTensorBackend"/> for inference (RunInference, shared-D3D12
/// session options) with <see cref="CpuTensorBackend"/> for SIMD arithmetic
/// (matmul / add).
/// </summary>
/// <remarks>
/// <para>
/// <b>Device reporting:</b> When constructed with an
/// <see cref="ISharedOrtDmlSessionFactory"/> that exposes an initialised D3D12
/// device, <see cref="Device"/> returns <see cref="DeviceType.DirectML"/> —
/// truthful even though arithmetic ops currently run on CPU SIMD. The intent
/// is to signal "GPU inference is available through me" so consumers route
/// avatar / Phi3v / Hermes-3 / Kokoro inference through the same shared device
/// instead of standing up parallel ORT environments.
/// </para>
/// <para>
/// <b>Why arithmetic stays on CPU SIMD:</b> ORT is a graph executor, not a
/// per-op tensor library. Each MatMul / Add invocation through ORT means
/// graph-compile + IOBinding + dispatch + sync — round-trip cost dominates
/// for small/medium tensors. <see cref="System.Numerics.Tensors.TensorPrimitives"/>
/// SIMD beats DML for the avatar pipeline's typical sizes. Real GPU-accelerated
/// arithmetic (pre-baked matmul.onnx / add.onnx graphs + IOBinding for zero-copy
/// device residency + a <c>DmlTensor : ITensor&lt;float&gt;</c> that holds GPU
/// memory) is a follow-up phase — meaningful enough that it warrants its own
/// roadmap entry.
/// </para>
/// <para>
/// <b>Inference path:</b> <see cref="RunInference"/> + <see cref="TryCreateSessionOptions"/>
/// pass through to the inner <see cref="OnnxRuntimeTensorBackend"/>, which
/// implements the ORT-GenAI-style circuit-breaker on session failures.
/// </para>
/// </remarks>
public sealed class OnnxTensorBackend : ITensorBackend, IDisposable
{
    private readonly OnnxRuntimeTensorBackend _ortInference;
    private readonly CpuTensorBackend _cpuArithmetic = CpuTensorBackend.Instance;
    private readonly ISharedOrtDmlSessionFactory? _sessionFactory;
    private readonly ILogger<OnnxTensorBackend> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnnxTensorBackend"/> class.
    /// </summary>
    /// <param name="sessionFactory">
    /// Optional shared D3D12 / DirectML session factory. When supplied AND the
    /// underlying device is initialised, <see cref="Device"/> reports
    /// <see cref="DeviceType.DirectML"/>. Pass <see langword="null"/> for a
    /// CPU-only configuration (still delegates inference through ORT — useful
    /// on hosts without a DirectX 12 GPU).
    /// </param>
    /// <param name="logger">Optional structured logger.</param>
    public OnnxTensorBackend(
        ISharedOrtDmlSessionFactory? sessionFactory = null,
        ILogger<OnnxTensorBackend>? logger = null)
    {
        _sessionFactory = sessionFactory;
        _logger = logger ?? NullLogger<OnnxTensorBackend>.Instance;
        _ortInference = new OnnxRuntimeTensorBackend(sessionFactory);

        _logger.LogInformation(
            "[OnnxTensorBackend] initialised — device={Device}, sessionFactory={Factory}",
            Device,
            _sessionFactory is null ? "none (CPU EP)" : "shared D3D12 DirectML");
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns <see cref="DeviceType.DirectML"/> when a shared D3D12 / DirectML
    /// device is available through the injected
    /// <see cref="ISharedOrtDmlSessionFactory"/> (which means inference will
    /// run on GPU); returns <see cref="DeviceType.Cpu"/> otherwise. Arithmetic
    /// ops always run on CPU SIMD regardless of this value — see class remarks.
    /// </remarks>
    public DeviceType Device =>
        _sessionFactory is not null ? DeviceType.DirectML : DeviceType.Cpu;

    /// <inheritdoc/>
    public ITensor<float> Create(TensorShape shape, ReadOnlySpan<float> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _cpuArithmetic.Create(shape, data);
    }

    /// <inheritdoc/>
    public ITensor<float> CreateUninitialized(TensorShape shape)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _cpuArithmetic.CreateUninitialized(shape);
    }

    /// <inheritdoc/>
    public ITensor<float> FromMemory(ReadOnlyMemory<float> memory, TensorShape shape)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _cpuArithmetic.FromMemory(memory, shape);
    }

    /// <inheritdoc/>
    public Result<ITensor<float>, string> MatMul(ITensor<float> a, ITensor<float> b)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _cpuArithmetic.MatMul(a, b);
    }

    /// <inheritdoc/>
    public Result<ITensor<float>, string> Add(ITensor<float> a, ITensor<float> b)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _cpuArithmetic.Add(a, b);
    }

    /// <summary>
    /// Runs an ONNX inference session against the supplied named inputs. Routes
    /// through the inner <see cref="OnnxRuntimeTensorBackend"/> so circuit-breaker
    /// and shared-D3D12 device wiring are honoured.
    /// </summary>
    public Result<IReadOnlyDictionary<string, ITensor<float>>, string> RunInference(
        InferenceSession session,
        IReadOnlyDictionary<string, ITensor<float>> inputs,
        IEnumerable<string>? outputNames = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _ortInference.RunInference(session, inputs, outputNames);
    }

    /// <summary>
    /// When a session factory was supplied at construction, returns
    /// <see cref="SessionOptions"/> bound to the shared D3D12 / DirectML device.
    /// Returns <see langword="null"/> when no factory is configured, or when the
    /// shared device is unavailable — caller should fall back to CPU EP.
    /// </summary>
    public SessionOptions? TryCreateSessionOptions()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _ortInference.TryCreateSessionOptions();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _ortInference.Dispose();
    }
}
