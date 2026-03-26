// <copyright file="RemoteTensorBackend.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http;
using Ouroboros.Tensor.Configuration;
using Ouroboros.Tensor.Models;

namespace Ouroboros.Tensor.Backends;

/// <summary>
/// ITensorBackend implementation that delegates operations to a remote Docker PyTorch service
/// for GPU-accelerated tensor operations (MatMul, FFT). Falls back to CPU when GPU unavailable.
/// </summary>
/// <remarks>
/// <para>
/// This backend connects to a FastAPI tensor service running in Docker with PyTorch ROCm,
/// enabling GPU acceleration on AMD GPUs. The HTTP API provides MatMul, FFT, and CosineSimilarity.
/// </para>
/// <para>
/// <see cref="Create"/> and <see cref="FromMemory"/> are local operations (zero-copy when possible).
/// Operations like <see cref="MatMul"/> and <see cref="FFT"/> send data to the remote service.
/// </para>
/// <para>
/// Device detection: Constructor calls /health endpoint to determine if GPU is available.
/// </para>
/// </remarks>
public sealed class RemoteTensorBackend : ITensorBackend, IDisposable
{
    private readonly TensorServiceClient _client;
    private readonly DeviceType _deviceType;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteTensorBackend"/> class.
    /// Calls the health endpoint to determine device availability.
    /// </summary>
    /// <param name="client">The tensor service HTTP client.</param>
    /// <param name="options">The tensor service configuration options.</param>
    public RemoteTensorBackend(TensorServiceClient client, TensorServiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        _client = client;

        // Synchronous health check to determine device type
        // This runs during DI resolution and blocks until complete
        try
        {
            var health = _client.GetHealthAsync(CancellationToken.None).GetAwaiter().GetResult();
            _deviceType = health.Device.Equals("cuda", StringComparison.OrdinalIgnoreCase)
                ? DeviceType.Cuda
                : DeviceType.Cpu;
        }
        catch (HttpRequestException)
        {
            // If health check fails, mark as CPU fallback
            // The service may become available later, or CpuTensorBackend will be used
            _deviceType = DeviceType.Cpu;
        }
        catch (TaskCanceledException)
        {
            // Timeout during health check
            _deviceType = DeviceType.Cpu;
        }
        catch (InvalidOperationException)
        {
            // Invalid response from health endpoint
            _deviceType = DeviceType.Cpu;
        }
    }

    /// <inheritdoc/>
    public DeviceType Device => _deviceType;

    /// <inheritdoc/>
    /// <remarks>
    /// Creates a pooled tensor by copying data from the provided span.
    /// This is a local operation (no HTTP call).
    /// </remarks>
    public ITensor<float> Create(TensorShape shape, ReadOnlySpan<float> data)
        => TensorMemoryPool.RentAndFill(shape, data);

    /// <inheritdoc/>
    /// <remarks>
    /// Creates an uninitialized pooled tensor. Local operation (no HTTP call).
    /// </remarks>
    public ITensor<float> CreateUninitialized(TensorShape shape)
        => TensorMemoryPool.Rent<float>(shape);

    /// <inheritdoc/>
    /// <remarks>
    /// Zero-copy wrapper around externally-owned memory. Local operation (no HTTP call).
    /// </remarks>
    public ITensor<float> FromMemory(ReadOnlyMemory<float> memory, TensorShape shape)
        => new ReadOnlyMemoryTensor<float>(memory, shape);

    /// <inheritdoc/>
    /// <remarks>
    /// Sends tensors to remote service for GPU-accelerated matrix multiplication.
    /// Returns a pooled tensor with the result.
    /// </remarks>
#pragma warning disable CA2000 // Dispose objects before losing scope - returned ITensor is owned by caller
    public Result<ITensor<float>, string> MatMul(ITensor<float> a, ITensor<float> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        // Validate shapes
        if (a.Shape.Rank < 2 || b.Shape.Rank < 2)
            return Result<ITensor<float>, string>.Failure(
                $"MatMul requires at least rank-2 tensors, got {a.Shape} and {b.Shape}.");

        var aRows = a.Shape.Dimensions[^2];
        var aCols = a.Shape.Dimensions[^1];
        var bRows = b.Shape.Dimensions[^2];
        var bCols = b.Shape.Dimensions[^1];

        if (aCols != bRows)
            return Result<ITensor<float>, string>.Failure(
                $"MatMul shape mismatch: [{aRows}x{aCols}] x [{bRows}x{bCols}] — inner dimensions must match.");

        try
        {
            // Convert to TensorData for HTTP transport
            var tensorA = ToTensorData(a);
            var tensorB = ToTensorData(b);

            // Call remote service (sync wrapper for async)
            var response = _client.MatMulAsync(tensorA, tensorB, CancellationToken.None)
                .GetAwaiter().GetResult();

            // Create result tensor from response
            var resultShape = TensorShape.Of(response.Shape.ToArray());
            var result = TensorMemoryPool.Rent<float>(resultShape);
            response.Data.CopyTo(result.WritableMemory.Span);

            return Result<ITensor<float>, string>.Success(result);
        }
        catch (HttpRequestException ex)
        {
            return Result<ITensor<float>, string>.Failure(
                $"MatMul remote call failed: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            return Result<ITensor<float>, string>.Failure(
                $"MatMul remote call timed out: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return Result<ITensor<float>, string>.Failure(
                $"MatMul invalid response: {ex.Message}");
        }
    }
#pragma warning restore CA2000

    /// <summary>
    /// Performs Fast Fourier Transform on the remote tensor service.
    /// </summary>
    /// <param name="input">Input tensor for FFT.</param>
    /// <param name="dimensions">Number of dimensions for FFT (default: 1).</param>
    /// <returns>FFT result tensor (complex values as float pairs).</returns>
    /// <remarks>
    /// This addresses TNS-02: TensorPrimitives does not provide FFT methods.
    /// GPU-accelerated FFT is essential for signal processing workloads.
    /// </remarks>
#pragma warning disable CA2000 // Dispose objects before losing scope - returned ITensor is owned by caller
    public Result<ITensor<float>, string> FFT(ITensor<float> input, int dimensions = 1)
    {
        ArgumentNullException.ThrowIfNull(input);

        try
        {
            // Convert to TensorData for HTTP transport
            var tensorInput = ToTensorData(input);

            // Call remote FFT endpoint
            var response = _client.FftAsync(tensorInput, dimensions, CancellationToken.None)
                .GetAwaiter().GetResult();

            // Create result tensor (complex output as float pairs)
            var resultShape = TensorShape.Of(response.Shape.ToArray());
            var result = TensorMemoryPool.Rent<float>(resultShape);
            response.Data.CopyTo(result.WritableMemory.Span);

            return Result<ITensor<float>, string>.Success(result);
        }
        catch (HttpRequestException ex)
        {
            return Result<ITensor<float>, string>.Failure(
                $"FFT remote call failed: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            return Result<ITensor<float>, string>.Failure(
                $"FFT remote call timed out: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return Result<ITensor<float>, string>.Failure(
                $"FFT invalid response: {ex.Message}");
        }
    }
#pragma warning restore CA2000

    /// <inheritdoc/>
    /// <remarks>
    /// Uses CpuTensorBackend for Add operations. The remote service can be extended
    /// with an Add endpoint if GPU acceleration becomes beneficial for large tensors.
    /// </remarks>
    public Result<ITensor<float>, string> Add(ITensor<float> a, ITensor<float> b)
    {
        // Add is fast on CPU with TensorPrimitives SIMD; delegate to CpuTensorBackend
        // Remote service can be extended with /tensor/add if needed for large tensors
        return CpuTensorBackend.Instance.Add(a, b);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed = true;
        // TensorServiceClient is injected — caller owns its lifetime.
        // This method exists so test teardown (IDisposable.Dispose) compiles.
    }

    /// <summary>
    /// Converts an ITensor to TensorData for HTTP transport.
    /// </summary>
    private static TensorData ToTensorData(ITensor<float> tensor)
    {
        var shape = tensor.Shape.Dimensions.ToArray();
        var data = tensor.AsSpan().ToArray();
        return new TensorData(shape.ToList(), data.ToList());
    }
}