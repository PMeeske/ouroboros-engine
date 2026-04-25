// <copyright file="TensorPipelineArrows.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Pipeline;

/// <summary>
/// Kleisli arrow factories for the streaming tensor pipeline stages: Decode → Normalize → Batch.
/// Each stage is a composable <see cref="Step{TInput,TOutput}"/> or
/// <see cref="KleisliResult{TInput,TOutput,TError}"/> that can be chained using <c>.Then()</c>
/// following the established Ouroboros functional composition pattern (R04, R10).
/// </summary>
/// <example>
/// <code>
/// var pipeline = TensorPipelineArrows.DecodeArrow(myDecoder)
///     .Then(TensorPipelineArrows.NormalizeArrow(mean: 0f, std: 1f))
///     .Then(TensorPipelineArrows.BatchToTensorArrow(backend, batchSize: 64));
///
/// var tensors = await pipeline(rawByteStream);
/// await foreach (var tensor in tensors)
///     ProcessBatch(tensor);
/// </code>
/// </example>
public static class TensorPipelineArrows
{
    // ────────────────────────────────────────────────────────────────────────────────
    // Individual stage arrows
    // ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a Decode arrow: <c>IAsyncEnumerable&lt;byte[]&gt; → IAsyncEnumerable&lt;float[]&gt;</c>.
    /// Applies <paramref name="decoder"/> to each chunk in the stream.
    /// </summary>
    /// <returns></returns>
    public static Step<IAsyncEnumerable<byte[]>, IAsyncEnumerable<float[]>> DecodeArrow(
        Func<byte[], float[]> decoder)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        return source => Task.FromResult(DecodeStream(source, decoder));
    }

    /// <summary>
    /// Creates a Normalize arrow: <c>IAsyncEnumerable&lt;float[]&gt; → IAsyncEnumerable&lt;float[]&gt;</c>.
    /// Applies z-score normalisation element-wise: <c>(x - mean) / std</c>.
    /// </summary>
    /// <param name="mean">Mean to subtract. Default 0 (no shift).</param>
    /// <param name="std">Standard deviation to divide by. Must not be zero.</param>
    /// <returns></returns>
    public static Step<IAsyncEnumerable<float[]>, IAsyncEnumerable<float[]>> NormalizeArrow(
        float mean = 0f, float std = 1f)
    {
        if (std == 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(std), "Standard deviation must not be zero.");
        }

        return source => Task.FromResult(NormalizeStream(source, mean, std));
    }

    /// <summary>
    /// Creates a BatchToTensor arrow:
    /// <c>IAsyncEnumerable&lt;float[]&gt; → IAsyncEnumerable&lt;ITensor&lt;float&gt;&gt;</c>.
    /// Accumulates vectors into batches of <paramref name="batchSize"/> and emits a 2-D tensor
    /// for each full batch, plus a partial tensor for the final batch.
    /// </summary>
    /// <returns></returns>
    public static Step<IAsyncEnumerable<float[]>, IAsyncEnumerable<ITensor<float>>> BatchToTensorArrow(
        ITensorBackend backend,
        int batchSize)
    {
        ArgumentNullException.ThrowIfNull(backend);
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive.");
        }

        return source => Task.FromResult(
            StreamingTensorAdapter.AdaptAsync(source, backend, batchSize));
    }

    // ────────────────────────────────────────────────────────────────────────────────
    // Safe end-to-end pipeline
    // ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a fully composed safe pipeline arrow that runs Decode → Normalize → Batch in one
    /// step. Errors during construction are captured in the <see cref="Result{TSuccess,TError}"/>
    /// return value; errors during async enumeration propagate as exceptions (R15).
    /// </summary>
    /// <returns></returns>
    public static KleisliResult<IAsyncEnumerable<byte[]>, IAsyncEnumerable<ITensor<float>>, string>
        SafeStreamingPipelineArrow(ITensorBackend backend, TensorPipelineConfig config)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(config);

        return source =>
        {
            try
            {
                config.Validate();

                var decoded = DecodeStream(source, config.Decoder);
                var normalized = NormalizeStream(decoded, config.NormalizationMean, config.NormalizationStd);
                var batched = StreamingTensorAdapter.AdaptAsync(normalized, backend, config.BatchSize);

                return Task.FromResult(
                    Result<IAsyncEnumerable<ITensor<float>>, string>.Success(batched));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Task.FromResult(
                    Result<IAsyncEnumerable<ITensor<float>>, string>.Failure(ex.Message));
            }
        };
    }

    // ────────────────────────────────────────────────────────────────────────────────
    // Private async helpers
    // ────────────────────────────────────────────────────────────────────────────────
    private static async IAsyncEnumerable<float[]> DecodeStream(
        IAsyncEnumerable<byte[]> source,
        Func<byte[], float[]> decoder,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var chunk in source.WithCancellation(ct).ConfigureAwait(false))
        {
            yield return decoder(chunk);
        }
    }

    private static async IAsyncEnumerable<float[]> NormalizeStream(
        IAsyncEnumerable<float[]> source,
        float mean,
        float std,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var vector in source.WithCancellation(ct).ConfigureAwait(false))
        {
            var normalized = new float[vector.Length];
            TensorPrimitives.Subtract(vector, mean, normalized);
            TensorPrimitives.Divide(normalized, std, normalized);
            yield return normalized;
        }
    }
}
