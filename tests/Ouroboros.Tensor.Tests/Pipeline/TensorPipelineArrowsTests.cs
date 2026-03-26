// <copyright file="TensorPipelineArrowsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;

namespace Ouroboros.Tests.Pipeline;

[Trait("Category", "Unit")]
public sealed class TensorPipelineArrowsTests
{
    private readonly CpuTensorBackend _backend = CpuTensorBackend.Instance;

    // ── DecodeArrow ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DecodeArrow_AppliesDecoderToEachChunk()
    {
        // Arrange
        var raw = new[] { new byte[] { 1 }, new byte[] { 2 } };
        var arrow = TensorPipelineArrows.DecodeArrow(b => new[] { (float)b[0] });

        // Act
        var decoded = await arrow(ToAsync(raw));
        var results = new List<float[]>();
        await foreach (var v in decoded) results.Add(v);

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().Equal(1f);
        results[1].Should().Equal(2f);
    }

    // ── NormalizeArrow ────────────────────────────────────────────────────────

    [Fact]
    public async Task NormalizeArrow_ZMeansNoShift_DividesByStd()
    {
        // Arrange
        var floats = new[] { new float[] { 2f, 4f, 6f } };
        var arrow = TensorPipelineArrows.NormalizeArrow(mean: 0f, std: 2f);

        // Act
        var normalized = await arrow(ToAsync(floats));
        var results = new List<float[]>();
        await foreach (var v in normalized) results.Add(v);

        // Assert
        results[0].Should().Equal(1f, 2f, 3f);
    }

    [Fact]
    public async Task NormalizeArrow_WithMeanAndStd_NormalizesCorrectly()
    {
        var floats = new[] { new float[] { 3f, 5f, 7f } };
        var arrow = TensorPipelineArrows.NormalizeArrow(mean: 2f, std: 2f);

        var normalized = await arrow(ToAsync(floats));
        var results = new List<float[]>();
        await foreach (var v in normalized) results.Add(v);

        // (3-2)/2 = 0.5, (5-2)/2 = 1.5, (7-2)/2 = 2.5
        results[0][0].Should().BeApproximately(0.5f, 0.001f);
        results[0][1].Should().BeApproximately(1.5f, 0.001f);
        results[0][2].Should().BeApproximately(2.5f, 0.001f);
    }

    [Fact]
    public void NormalizeArrow_ZeroStd_ThrowsArgumentOutOfRangeException()
    {
        var act = () => TensorPipelineArrows.NormalizeArrow(std: 0f);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ── BatchToTensorArrow ────────────────────────────────────────────────────

    [Fact]
    public async Task BatchToTensorArrow_EmitsBatchedTensors()
    {
        // Arrange
        var floats = new[]
        {
            new float[] { 1f, 2f },
            new float[] { 3f, 4f },
            new float[] { 5f, 6f },
        };

        var arrow = TensorPipelineArrows.BatchToTensorArrow(_backend, batchSize: 2);

        // Act
        var tensors = new List<ITensor<float>>();
        var stream = await arrow(ToAsync(floats));
        await foreach (var t in stream) tensors.Add(t);

        // Assert: 2 full + 1 partial
        tensors.Should().HaveCount(2);
        tensors[0].Shape.Should().Be(TensorShape.Of(2, 2));
        tensors[1].Shape.Should().Be(TensorShape.Of(1, 2));

        foreach (var t in tensors) t.Dispose();
    }

    // ── SafeStreamingPipelineArrow ────────────────────────────────────────────

    [Fact]
    public async Task SafeStreamingPipelineArrow_EndToEnd_ProducesCorrectTensors()
    {
        // Arrange: encode 2-element float vectors as raw bytes
        float[][] vectors = { new[] { 1f, 2f }, new[] { 3f, 4f } };
        byte[][] raw = vectors.Select(v =>
        {
            var bytes = new byte[v.Length * sizeof(float)];
            Buffer.BlockCopy(v, 0, bytes, 0, bytes.Length);
            return bytes;
        }).ToArray();

        var config = new TensorPipelineConfig(
            BatchSize: 2,
            Decoder: TensorPipelineConfig.Default.Decoder,
            NormalizationMean: 0f,
            NormalizationStd: 1f);

        var arrow = TensorPipelineArrows.SafeStreamingPipelineArrow(_backend, config);

        // Act
        var result = await arrow(ToAsync(raw));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var tensors = new List<ITensor<float>>();
        await foreach (var t in result.Value) tensors.Add(t);

        tensors.Should().HaveCount(1);
        tensors[0].Shape.Should().Be(TensorShape.Of(2, 2));
        foreach (var t in tensors) t.Dispose();
    }

    [Fact]
    public async Task SafeStreamingPipelineArrow_InvalidConfig_ReturnsFailure()
    {
        // BatchSize = 0 triggers Validate()
        var config = new TensorPipelineConfig(BatchSize: 0, Decoder: b => new float[0]);
        var arrow = TensorPipelineArrows.SafeStreamingPipelineArrow(_backend, config);

        var result = await arrow(ToAsync(Array.Empty<byte[]>()));

        result.IsSuccess.Should().BeFalse();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async IAsyncEnumerable<T> ToAsync<T>(
        T[] items,
        [EnumeratorCancellation] CancellationToken _ = default)
    {
        foreach (var item in items)
            yield return await Task.FromResult(item);
    }
}
