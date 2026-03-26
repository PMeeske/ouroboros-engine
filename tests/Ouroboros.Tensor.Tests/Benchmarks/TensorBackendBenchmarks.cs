// <copyright file="TensorBackendBenchmarks.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http;
using System.Numerics;
using System.Numerics.Tensors;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Backends;
using Ouroboros.Tensor.Configuration;
using Ouroboros.Tensor.Memory;

namespace Ouroboros.Tensor.Tests.Benchmarks;

/// <summary>
/// Performance benchmarks for tensor backends (TNS-04 verification).
/// Compares naive CPU, TensorPrimitives SIMD, and RemoteTensorBackend GPU operations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net100)]
public class TensorBackendBenchmarks
{
    private CpuTensorBackend _cpuBackend = null!;
    private RemoteTensorBackend? _remoteBackend;
    private float[] _inputSmall = null!;
    private float[] _inputMedium = null!;
    private float[] _inputLarge = null!;
    private float[] _weightsSmall = null!;
    private float[] _weightsMedium = null!;
    private float[] _weightsLarge = null!;
    private float[] _vectorA = null!;
    private float[] _vectorB = null!;
    private float[] _signalSmall = null!;
    private float[] _signalMedium = null!;
    private float[] _signalLarge = null!;
    private float[] _kernelSmall = null!;
    private float[] _kernelMedium = null!;
    private float[] _kernelLarge = null!;

    // Matrix dimensions for MatMul benchmarks
    private const int SmallMatrixSize = 64;
    private const int MediumMatrixSize = 256;
    private const int LargeMatrixSize = 1024;

    // Vector dimensions for CosineSimilarity and Normalize benchmarks
    private const int SmallVectorSize = 64;
    private const int MediumVectorSize = 256;
    private const int LargeVectorSize = 1024;

    /// <summary>
    /// Global setup for benchmarks.
    /// Initializes test data and backends.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        // Initialize CPU backend
        _cpuBackend = CpuTensorBackend.Instance;

        // Initialize RemoteTensorBackend for GPU benchmarks (skipped if service unavailable)
        try
        {
            var options = new TensorServiceOptions
            {
                BaseUrl = new Uri("http://localhost:8768"),
                TimeoutSeconds = 5,
                MaxRetryAttempts = 1
            };
            var httpClient = new HttpClient
            {
                BaseAddress = options.BaseUrl,
                Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
            };
            var client = new TensorServiceClient(httpClient, options);
            _remoteBackend = new RemoteTensorBackend(client, options);
        }
        catch
        {
            // Service not available — GPU benchmarks will be skipped
            _remoteBackend = null;
        }

        // Initialize test vectors with random data
        var random = new Random(42);

        // Small vectors
        _inputSmall = InitializeRandomArray(SmallMatrixSize * SmallMatrixSize, random);
        _weightsSmall = InitializeRandomArray(SmallMatrixSize * SmallMatrixSize, random);
        _vectorA = InitializeRandomArray(SmallVectorSize, random);
        _vectorB = InitializeRandomArray(SmallVectorSize, random);
        _signalSmall = InitializeRandomArray(SmallVectorSize, random);
        _kernelSmall = InitializeRandomArray(SmallVectorSize, random);

        // Medium vectors
        _inputMedium = InitializeRandomArray(MediumMatrixSize * MediumMatrixSize, random);
        _weightsMedium = InitializeRandomArray(MediumMatrixSize * MediumMatrixSize, random);
        _signalMedium = InitializeRandomArray(MediumVectorSize, random);
        _kernelMedium = InitializeRandomArray(MediumVectorSize, random);

        // Large vectors
        _inputLarge = InitializeRandomArray(LargeMatrixSize * LargeMatrixSize, random);
        _weightsLarge = InitializeRandomArray(LargeMatrixSize * LargeMatrixSize, random);
        _signalLarge = InitializeRandomArray(LargeVectorSize, random);
        _kernelLarge = InitializeRandomArray(LargeVectorSize, random);
    }

    #region CosineSimilarity Benchmarks

    /// <summary>
    /// Naive CPU cosine similarity implementation (baseline).
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CosineSimilarity", "Small")]
    public float CosineSimilarity_Naive_Small()
    {
        float dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < _vectorA.Length; i++)
        {
            dot += _vectorA[i] * _vectorB[i];
            normA += _vectorA[i] * _vectorA[i];
            normB += _vectorB[i] * _vectorB[i];
        }
        return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    /// <summary>
    /// TensorPrimitives SIMD-accelerated cosine similarity (TNS-03).
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("CosineSimilarity", "Small")]
    public float CosineSimilarity_TensorPrimitives_Small()
    {
        return TensorPrimitives.CosineSimilarity(_vectorA, _vectorB);
    }

    /// <summary>
    /// TensorPrimitives SIMD-accelerated cosine similarity - medium size.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("CosineSimilarity", "Medium")]
    public float CosineSimilarity_TensorPrimitives_Medium()
    {
        return TensorPrimitives.CosineSimilarity(_inputMedium.AsSpan(0, MediumVectorSize), _inputMedium.AsSpan(MediumVectorSize, MediumVectorSize));
    }

    /// <summary>
    /// TensorPrimitives SIMD-accelerated cosine similarity - large size.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("CosineSimilarity", "Large")]
    public float CosineSimilarity_TensorPrimitives_Large()
    {
        return TensorPrimitives.CosineSimilarity(_inputLarge.AsSpan(0, LargeVectorSize), _inputLarge.AsSpan(LargeVectorSize, LargeVectorSize));
    }

    #endregion

    #region Normalize Benchmarks

    /// <summary>
    /// Naive CPU normalize implementation (baseline).
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Normalize", "Small")]
    public void Normalize_Naive_Small()
    {
        float[] result = new float[_vectorA.Length];
        float norm = 0;
        for (int i = 0; i < _vectorA.Length; i++)
        {
            norm += _vectorA[i] * _vectorA[i];
        }
        norm = MathF.Sqrt(norm);
        for (int i = 0; i < _vectorA.Length; i++)
        {
            result[i] = _vectorA[i] / norm;
        }
    }

    /// <summary>
    /// TensorPrimitives SIMD-accelerated normalize (TNS-03).
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Normalize", "Small")]
    public void Normalize_TensorPrimitives_Small()
    {
        float[] result = new float[_vectorA.Length];
        TensorPrimitives.Divide(_vectorA, MathF.Sqrt(TensorPrimitives.SumOfSquares(_vectorA)), result);
    }

    /// <summary>
    /// TensorPrimitives SIMD-accelerated normalize - medium size.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Normalize", "Medium")]
    public void Normalize_TensorPrimitives_Medium()
    {
        Span<float> source = _inputMedium.AsSpan(0, MediumVectorSize);
        float[] result = new float[MediumVectorSize];
        TensorPrimitives.Divide(source, MathF.Sqrt(TensorPrimitives.SumOfSquares(source)), result);
    }

    /// <summary>
    /// TensorPrimitives SIMD-accelerated normalize - large size.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Normalize", "Large")]
    public void Normalize_TensorPrimitives_Large()
    {
        Span<float> source = _inputLarge.AsSpan(0, LargeVectorSize);
        float[] result = new float[LargeVectorSize];
        TensorPrimitives.Divide(source, MathF.Sqrt(TensorPrimitives.SumOfSquares(source)), result);
    }

    #endregion

    #region MatMul Benchmarks

    /// <summary>
    /// Naive O(n^3) matrix multiplication (baseline).
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("MatMul", "Small")]
    public float[] MatMul_Naive_Small()
    {
        return NaiveMatMul(_inputSmall, _weightsSmall, SmallMatrixSize, SmallMatrixSize, SmallMatrixSize);
    }

    /// <summary>
    /// CpuTensorBackend with TensorPrimitives SIMD (TNS-01).
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("MatMul", "Small")]
    public ITensor<float> MatMul_CpuTensorBackend_Small()
    {
        var a = _cpuBackend.Create(TensorShape.Of(SmallMatrixSize, SmallMatrixSize), _inputSmall);
        var b = _cpuBackend.Create(TensorShape.Of(SmallMatrixSize, SmallMatrixSize), _weightsSmall);
        var result = _cpuBackend.MatMul(a, b);
        a.Dispose();
        b.Dispose();
        return result.Value;
    }

    /// <summary>
    /// CpuTensorBackend MatMul - medium size.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("MatMul", "Medium")]
    public ITensor<float> MatMul_CpuTensorBackend_Medium()
    {
        var a = _cpuBackend.Create(TensorShape.Of(MediumMatrixSize, MediumMatrixSize), _inputMedium);
        var b = _cpuBackend.Create(TensorShape.Of(MediumMatrixSize, MediumMatrixSize), _weightsMedium);
        var result = _cpuBackend.MatMul(a, b);
        a.Dispose();
        b.Dispose();
        return result.Value;
    }

    /// <summary>
    /// CpuTensorBackend MatMul - large size.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("MatMul", "Large")]
    public ITensor<float> MatMul_CpuTensorBackend_Large()
    {
        var a = _cpuBackend.Create(TensorShape.Of(LargeMatrixSize, LargeMatrixSize), _inputLarge);
        var b = _cpuBackend.Create(TensorShape.Of(LargeMatrixSize, LargeMatrixSize), _weightsLarge);
        var result = _cpuBackend.MatMul(a, b);
        a.Dispose();
        b.Dispose();
        return result.Value;
    }

    #endregion

    #region FFT Benchmarks

    /// <summary>
    /// Naive CPU FFT-based convolution (baseline).
    /// Uses Cooley-Tukey FFT for O(n log n) complexity.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("FFT", "Small")]
    public float[] FFT_Cpu_Small()
    {
        return NaiveFftConvolve(_signalSmall, _kernelSmall);
    }

    /// <summary>
    /// Naive CPU FFT convolution - medium size.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("FFT", "Medium")]
    public float[] FFT_Cpu_Medium()
    {
        return NaiveFftConvolve(_signalMedium, _kernelMedium);
    }

    /// <summary>
    /// Naive CPU FFT convolution - large size.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("FFT", "Large")]
    public float[] FFT_Cpu_Large()
    {
        return NaiveFftConvolve(_signalLarge, _kernelLarge);
    }

    #endregion

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _remoteBackend?.Dispose();
    }

    #region RemoteTensorBackend GPU Benchmarks (TNS-04)

    /// <summary>
    /// RemoteTensorBackend GPU MatMul - small (64x64). Requires Docker tensor service on port 8768.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("MatMul", "GPU", "Small")]
    public ITensor<float>? MatMul_RemoteGpu_Small()
    {
        if (_remoteBackend is null) return null;
        var a = _remoteBackend.Create(TensorShape.Of(SmallMatrixSize, SmallMatrixSize), _inputSmall);
        var b = _remoteBackend.Create(TensorShape.Of(SmallMatrixSize, SmallMatrixSize), _weightsSmall);
        var result = _remoteBackend.MatMul(a, b);
        a.Dispose(); b.Dispose();
        return result.IsSuccess ? result.Value : null;
    }

    /// <summary>
    /// RemoteTensorBackend GPU MatMul - medium (256x256). Requires Docker tensor service on port 8768.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("MatMul", "GPU", "Medium")]
    public ITensor<float>? MatMul_RemoteGpu_Medium()
    {
        if (_remoteBackend is null) return null;
        var a = _remoteBackend.Create(TensorShape.Of(MediumMatrixSize, MediumMatrixSize), _inputMedium);
        var b = _remoteBackend.Create(TensorShape.Of(MediumMatrixSize, MediumMatrixSize), _weightsMedium);
        var result = _remoteBackend.MatMul(a, b);
        a.Dispose(); b.Dispose();
        return result.IsSuccess ? result.Value : null;
    }

    /// <summary>
    /// RemoteTensorBackend GPU MatMul - large (1024x1024). Requires Docker tensor service on port 8768.
    /// This is the primary >=5x speedup measurement for TNS-04.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("MatMul", "GPU", "Large")]
    public ITensor<float>? MatMul_RemoteGpu_Large()
    {
        if (_remoteBackend is null) return null;
        var a = _remoteBackend.Create(TensorShape.Of(LargeMatrixSize, LargeMatrixSize), _inputLarge);
        var b = _remoteBackend.Create(TensorShape.Of(LargeMatrixSize, LargeMatrixSize), _weightsLarge);
        var result = _remoteBackend.MatMul(a, b);
        a.Dispose(); b.Dispose();
        return result.IsSuccess ? result.Value : null;
    }

    /// <summary>
    /// RemoteTensorBackend GPU FFT - small (64 elements). Requires Docker tensor service on port 8768.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("FFT", "GPU", "Small")]
    public ITensor<float>? FFT_RemoteGpu_Small()
    {
        if (_remoteBackend is null) return null;
        var input = _remoteBackend.Create(TensorShape.Of(SmallVectorSize), _signalSmall);
        var result = _remoteBackend.FFT(input, dimensions: 1);
        input.Dispose();
        return result.IsSuccess ? result.Value : null;
    }

    /// <summary>
    /// RemoteTensorBackend GPU FFT - medium (256 elements). Requires Docker tensor service on port 8768.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("FFT", "GPU", "Medium")]
    public ITensor<float>? FFT_RemoteGpu_Medium()
    {
        if (_remoteBackend is null) return null;
        var input = _remoteBackend.Create(TensorShape.Of(MediumVectorSize), _signalMedium);
        var result = _remoteBackend.FFT(input, dimensions: 1);
        input.Dispose();
        return result.IsSuccess ? result.Value : null;
    }

    /// <summary>
    /// RemoteTensorBackend GPU FFT - large (1024 elements). Requires Docker tensor service on port 8768.
    /// This is the primary >=5x speedup measurement for TNS-04 FFT path.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("FFT", "GPU", "Large")]
    public ITensor<float>? FFT_RemoteGpu_Large()
    {
        if (_remoteBackend is null) return null;
        var input = _remoteBackend.Create(TensorShape.Of(LargeVectorSize), _signalLarge);
        var result = _remoteBackend.FFT(input, dimensions: 1);
        input.Dispose();
        return result.IsSuccess ? result.Value : null;
    }

    #endregion

    #region Helper Methods

    private static float[] InitializeRandomArray(int size, Random random)
    {
        float[] result = new float[size];
        for (int i = 0; i < size; i++)
        {
            result[i] = (float)(random.NextDouble() * 2 - 1);
        }
        return result;
    }

    /// <summary>
    /// Naive O(n^3) matrix multiplication.
    /// </summary>
    private static float[] NaiveMatMul(float[] a, float[] b, int rowsA, int colsA, int colsB)
    {
        float[] result = new float[rowsA * colsB];
        for (int i = 0; i < rowsA; i++)
        {
            for (int j = 0; j < colsB; j++)
            {
                float sum = 0;
                for (int k = 0; k < colsA; k++)
                {
                    sum += a[i * colsA + k] * b[k * colsB + j];
                }
                result[i * colsB + j] = sum;
            }
        }
        return result;
    }

    /// <summary>
    /// Naive FFT-based convolution using Cooley-Tukey FFT.
    /// </summary>
    private static float[] NaiveFftConvolve(float[] signal, float[] kernel)
    {
        int n = signal.Length;
        Complex[] signalComplex = new Complex[n];
        Complex[] kernelComplex = new Complex[n];

        for (int i = 0; i < n; i++)
        {
            signalComplex[i] = new Complex(signal[i], 0);
            kernelComplex[i] = new Complex(kernel[i], 0);
        }

        Fft(signalComplex);
        Fft(kernelComplex);

        for (int i = 0; i < n; i++)
        {
            signalComplex[i] *= kernelComplex[i];
        }

        InverseFft(signalComplex);

        float[] result = new float[n];
        for (int i = 0; i < n; i++)
        {
            result[i] = (float)signalComplex[i].Real;
        }
        return result;
    }

    /// <summary>
    /// In-place Cooley-Tukey FFT.
    /// </summary>
    private static void Fft(Complex[] data)
    {
        int n = data.Length;
        if (n <= 1) return;

        // Bit-reversal permutation
        int j = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                (data[i], data[j]) = (data[j], data[i]);
            }
            int k = n / 2;
            while (k <= j)
            {
                j -= k;
                k /= 2;
            }
            j += k;
        }

        // Cooley-Tukey FFT
        for (int len = 2; len <= n; len *= 2)
        {
            double angle = -2.0 * Math.PI / len;
            Complex wLen = new Complex(Math.Cos(angle), Math.Sin(angle));

            for (int i = 0; i < n; i += len)
            {
                Complex w = Complex.One;
                for (int k = 0; k < len / 2; k++)
                {
                    Complex t = w * data[i + k + len / 2];
                    Complex u = data[i + k];
                    data[i + k] = u + t;
                    data[i + k + len / 2] = u - t;
                    w *= wLen;
                }
            }
        }
    }

    /// <summary>
    /// In-place inverse FFT.
    /// </summary>
    private static void InverseFft(Complex[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = Complex.Conjugate(data[i]);
        }

        Fft(data);

        for (int i = 0; i < data.Length; i++)
        {
            data[i] = Complex.Conjugate(data[i]) / data.Length;
        }
    }

    #endregion
}