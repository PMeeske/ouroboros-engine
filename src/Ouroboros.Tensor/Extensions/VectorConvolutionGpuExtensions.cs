// <copyright file="VectorConvolutionGpuExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Numerics;
using Ouroboros.Core.Vectors;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Backends;
using Ouroboros.Tensor.Memory;

namespace Ouroboros.Tensor.Extensions;

/// <summary>
/// GPU-accelerated convolution operations companion to <see cref="VectorConvolution"/>.
/// Uses <see cref="RemoteTensorBackend.FFT"/> for FFT-based convolution (TNS-02).
/// </summary>
/// <remarks>
/// <para>
/// These methods provide GPU-accelerated operations when a <see cref="RemoteTensorBackend"/>
/// is available. They fall back to CPU implementations when the backend is null, CPU-only,
/// or not a RemoteTensorBackend.
/// </para>
/// <para>
/// Architecture note: This class is in Ouroboros.Tensor (engine layer) because
/// ITensorBackend lives here. VectorConvolution (Core, foundation layer) cannot reference
/// ITensorBackend, so GPU acceleration is provided in this companion class.
/// </para>
/// </remarks>
public static class VectorConvolutionGpu
{
    /// <summary>
    /// GPU-accelerated circular convolution using FFT.
    /// Uses <see cref="RemoteTensorBackend.FFT"/> when available, falls back to CPU FFT.
    /// </summary>
    /// <param name="signal">First input vector (signal).</param>
    /// <param name="kernel">Second input vector (kernel).</param>
    /// <param name="backend">Optional tensor backend for GPU acceleration. If null or not RemoteTensorBackend, uses CPU FFT.</param>
    /// <returns>Convolved vector (real parts of the complex result).</returns>
    /// <remarks>
    /// For GPU acceleration, the backend must be a <see cref="RemoteTensorBackend"/> with <see cref="DeviceType.Cuda"/>.
    /// FFT complexity: O(n log n) vs O(n^2) for naive convolution.
    /// GPU acceleration is beneficial for large vectors (typically >= 512 elements).
    /// </remarks>
    public static float[] FastCircularConvolve(
        float[] signal,
        float[] kernel,
        ITensorBackend? backend = null)
    {
        if (signal.Length != kernel.Length)
        {
            throw new ArgumentException("Vectors must have same dimension");
        }

        // If backend is RemoteTensorBackend with GPU, use GPU FFT
        if (backend is RemoteTensorBackend remoteBackend && backend.Device == DeviceType.Cuda)
        {
            return FastCircularConvolveViaGpuFft(signal, kernel, remoteBackend);
        }

        // Fallback to CPU FFT
        return VectorConvolution.FastCircularConvolve(signal, kernel);
    }

    /// <summary>
    /// GPU-accelerated FFT convolution using the tensor backend.
    /// Implements convolution theorem: conv(a, b) = IFFT(FFT(a) * FFT(b)).
    /// </summary>
    private static float[] FastCircularConvolveViaGpuFft(float[] signal, float[] kernel, RemoteTensorBackend backend)
    {
        int n = signal.Length;

        // Convert to complex for FFT (real values, zero imaginary)
        float[] signalComplex = new float[n * 2]; // interleaved real, imag
        float[] kernelComplex = new float[n * 2];

        for (int i = 0; i < n; i++)
        {
            signalComplex[i * 2] = signal[i];
            signalComplex[(i * 2) + 1] = 0;
            kernelComplex[i * 2] = kernel[i];
            kernelComplex[(i * 2) + 1] = 0;
        }

        // Create tensors for FFT
        var signalShape = TensorShape.Of(n, 2); // [n, 2] for complex (real, imag interleaved)
        var signalTensor = backend.Create(signalShape, signalComplex);
        var kernelTensor = backend.Create(signalShape, kernelComplex);

        try
        {
            // FFT both signal and kernel
            var signalFftResult = backend.FFT(signalTensor, dimensions: 1);
            var kernelFftResult = backend.FFT(kernelTensor, dimensions: 1);

            if (signalFftResult.IsFailure || kernelFftResult.IsFailure)
            {
                // Fallback to CPU if FFT fails
                return VectorConvolution.FastCircularConvolve(signal, kernel);
            }

            using var signalFft = signalFftResult.Value;
            using var kernelFft = kernelFftResult.Value;

            // Pointwise multiply in frequency domain
            using var product = PointwiseMultiplyComplex(signalFft, kernelFft);

            // Inverse FFT (FFT on conjugate gives IFFT)
            var ifftResult = backend.FFT(product, dimensions: 1);

            if (ifftResult.IsFailure)
            {
                return VectorConvolution.FastCircularConvolve(signal, kernel);
            }

            using var ifft = ifftResult.Value;

            // Extract real parts (scaled by 1/n for IFFT)
            float[] result = new float[n];
            var ifftSpan = ifft.AsSpan();

            for (int i = 0; i < n; i++)
            {
                // Real part is at index 2*i, need to scale for IFFT
                result[i] = ifftSpan[i * 2] / n;
            }

            return result;
        }
        finally
        {
            signalTensor.Dispose();
            kernelTensor.Dispose();
        }
    }

    /// <summary>
    /// Pointwise multiplication of complex tensors (interleaved real/imag).
    /// </summary>
    private static ITensor<float> PointwiseMultiplyComplex(ITensor<float> a, ITensor<float> b)
    {
        var shape = a.Shape;
        var result = TensorMemoryPool.Rent<float>(shape);
        var aSpan = a.AsSpan();
        var bSpan = b.AsSpan();
        var resultSpan = result.WritableMemory.Span;

        // Complex multiplication: (a.re + j*a.im) * (b.re + j*b.im)
        for (int i = 0; i < aSpan.Length / 2; i++)
        {
            float aRe = aSpan[i * 2];
            float aIm = aSpan[(i * 2) + 1];
            float bRe = bSpan[i * 2];
            float bIm = bSpan[(i * 2) + 1];

            resultSpan[i * 2] = (aRe * bRe) - (aIm * bIm);     // real part
            resultSpan[(i * 2) + 1] = (aRe * bIm) + (aIm * bRe);  // imag part
        }

        return result;
    }
}
